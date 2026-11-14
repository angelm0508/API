using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core.Reportes.Compartido
{
    /// <summary>Una línea de factura o nota de débito con su venta neta (sin IVA) y su costo.</summary>
    /// <param name="Tipo">«Factura» o «Nota de débito».</param>
    /// <param name="CantidadBase">Cantidad en la unidad base del artículo.</param>
    /// <param name="VentaNeta">TotalLinea − Impuesto (neto con descuento, sin IVA).</param>
    /// <param name="Costo">Costo de la cantidad vendida, tomado del inventario (0 si no hay movimientos).</param>
    public sealed record LineaVenta(
        int DocEntry, int NoLinea, DateTime? Fecha, int Serie, int NumDoc, string Tipo,
        string? CodigoSn, string? NombreSn, string? Moneda,
        string CodArticulo, string? NombreArticulo, string? Grupo, string? CodAlmacen,
        decimal CantidadBase, decimal VentaNeta, decimal PrctjeDesc, decimal Costo);

    /// <summary>Filtros de la consulta; <c>HastaExclusiva</c> es un límite superior exclusivo (&lt;).</summary>
    public sealed record FiltroLineasVenta(
        DateTime? Desde, DateTime? HastaExclusiva, string? Cliente, string? Articulo, string? Almacen, string? Grupo);

    /// <summary>
    /// Consulta compartida de los reportes de ventas: líneas de facturas y notas de débito no canceladas, con venta neta
    /// y costo. El costo sale de los movimientos de inventario de la «línea de origen» (la factura, o la entrega o la orden
    /// de servicio de la que se copió la línea, porque esos documentos fueron los que movieron el stock).
    /// </summary>
    public sealed class ConsultaLineasVenta
    {
        public const int MaxLineas = 200000;

        private const string TipoDocFactura = "6";
        private const string TipoDocEntrega = "5";
        private const string TipoDocOrdenServicio = "70";
        private const int BaseEntrega = 5;
        private const int BaseOrdenServicio = 70;

        private readonly IRepositorioGenerico<Factura, int> _facturas;
        private readonly IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> _detalles;
        private readonly IRepositorioGenerico<MovimientoInventario, int> _movimientos;

        public ConsultaLineasVenta(
            IRepositorioGenerico<Factura, int> facturas,
            IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> detalles,
            IRepositorioGenerico<MovimientoInventario, int> movimientos)
        {
            _facturas = facturas;
            _detalles = detalles;
            _movimientos = movimientos;
        }

        private readonly record struct Acumulado(decimal Valor, decimal Sale, decimal Entra);

        public async Task<IReadOnlyList<LineaVenta>> ObtenerAsync(FiltroLineasVenta filtro)
        {
            // 1. Facturas válidas (subconsulta, no lista materializada).
            var validas = (await _facturas.ObtenerTodoAsync())
                .Where(f => f.Cancelado != "S" && (f.DocSubType == "--" || f.DocSubType == "ND"));
            if (filtro.Desde is { } desde) validas = validas.Where(f => f.FechaDoc >= desde);
            if (filtro.HastaExclusiva is { } hasta) validas = validas.Where(f => f.FechaDoc < hasta);
            if (filtro.Cliente is { } cliente) validas = validas.Where(f => f.CodigoSn == cliente);

            // 2. Líneas con artículo de esas facturas, con los filtros opcionales.
            var detalles = (await _detalles.ObtenerTodoAsync()).Where(d => d.CodArticulo != null);
            if (filtro.Articulo is { } articulo) detalles = detalles.Where(d => d.CodArticulo == articulo);
            if (filtro.Almacen is { } almacen) detalles = detalles.Where(d => d.CodAlmacen == almacen);
            if (filtro.Grupo is { } grupo)
                detalles = detalles.Where(d => d.CodArticuloNavigation != null
                                            && d.CodArticuloNavigation.CodigoGrupoNavigation != null
                                            && d.CodArticuloNavigation.CodigoGrupoNavigation.Nombre!.Contains(grupo));

            // El join con las facturas válidas (en SQL equivale a la subconsulta por Entry) evita materializar listas.
            var lineas = from d in detalles join f in validas on d.Entry equals f.Entry select new { d, f };

            var filas = await lineas
                .OrderBy(x => x.f.FechaDoc).ThenBy(x => x.d.Entry).ThenBy(x => x.d.NoLinea)
                .Select(x => new
                {
                    x.d.Entry,
                    x.d.NoLinea,
                    x.f.FechaDoc,
                    x.f.Serie,
                    x.f.NumDoc,
                    x.f.DocSubType,
                    x.f.CodigoSn,
                    x.f.NombreSn,
                    x.f.MonedaDoc,
                    x.d.CodArticulo,
                    NombreArticulo = x.d.CodArticuloNavigation != null ? x.d.CodArticuloNavigation.Nombre : null,
                    Grupo = x.d.CodArticuloNavigation != null && x.d.CodArticuloNavigation.CodigoGrupoNavigation != null
                        ? x.d.CodArticuloNavigation.CodigoGrupoNavigation.Nombre : null,
                    x.d.CodAlmacen,
                    x.d.Cantidad,
                    x.d.CantidadBase,
                    x.d.TotalLinea,
                    x.d.Impuesto,
                    x.d.PrctjeDesc,
                    x.d.BaseTipo,
                    x.d.BaseEntry,
                    x.d.BaseLinea
                })
                .Take(MaxLineas + 1)
                .ToListAsync();

            if (filas.Count > MaxLineas)
                throw new ReporteInvalidoException("El período tiene demasiadas líneas para este reporte. Acorta el rango de fechas.");
            if (filas.Count == 0)
                return Array.Empty<LineaVenta>();

            // 3. Movimientos de inventario de las líneas de origen: una consulta por tipo de documento de origen.
            var movimientos = await _movimientos.ObtenerTodoAsync();
            var acumulados = new Dictionary<(string TipoDoc, int DocEntry, int DocLinea), Acumulado>();

            var propias = await movimientos
                .Where(m => m.TipoDoc == TipoDocFactura
                    && lineas.Where(x => x.d.BaseTipo != BaseEntrega && x.d.BaseTipo != BaseOrdenServicio).Select(x => x.d.Entry).Contains(m.DocEntry))
                .Select(m => new { m.TipoDoc, m.DocEntry, m.DocLinea, m.ValorMovimiento, m.CantidadSale, m.CantidadEntra })
                .ToListAsync();
            var deEntrega = await movimientos
                .Where(m => m.TipoDoc == TipoDocEntrega
                    && lineas.Where(x => x.d.BaseTipo == BaseEntrega).Select(x => x.d.BaseEntry).Contains(m.DocEntry))
                .Select(m => new { m.TipoDoc, m.DocEntry, m.DocLinea, m.ValorMovimiento, m.CantidadSale, m.CantidadEntra })
                .ToListAsync();
            var deOrden = await movimientos
                .Where(m => m.TipoDoc == TipoDocOrdenServicio
                    && lineas.Where(x => x.d.BaseTipo == BaseOrdenServicio).Select(x => x.d.BaseEntry).Contains(m.DocEntry))
                .Select(m => new { m.TipoDoc, m.DocEntry, m.DocLinea, m.ValorMovimiento, m.CantidadSale, m.CantidadEntra })
                .ToListAsync();

            foreach (var m in propias.Concat(deEntrega).Concat(deOrden))
            {
                var clave = (m.TipoDoc, m.DocEntry, m.DocLinea);
                acumulados.TryGetValue(clave, out var a);
                acumulados[clave] = new Acumulado(a.Valor + m.ValorMovimiento, a.Sale + m.CantidadSale, a.Entra + m.CantidadEntra);
            }

            // 4. Venta neta y costo por línea.
            var resultado = new List<LineaVenta>(filas.Count);
            foreach (var l in filas)
            {
                var cantidadBase = l.CantidadBase ?? l.Cantidad ?? 0m;
                var costo = 0m;
                if (TryClaveOrigen(l.BaseTipo, l.BaseEntry, l.BaseLinea, l.Entry, l.NoLinea, out var clave)
                    && acumulados.TryGetValue(clave, out var acum)
                    && acum.Sale - acum.Entra > 0m)
                {
                    costo = cantidadBase * (-acum.Valor / (acum.Sale - acum.Entra));
                }

                resultado.Add(new LineaVenta(
                    l.Entry, l.NoLinea, l.FechaDoc, l.Serie, l.NumDoc,
                    l.DocSubType == "ND" ? "Nota de débito" : "Factura",
                    l.CodigoSn, l.NombreSn, l.MonedaDoc,
                    l.CodArticulo!, l.NombreArticulo, l.Grupo, l.CodAlmacen,
                    cantidadBase,
                    Math.Round((l.TotalLinea ?? 0m) - (l.Impuesto ?? 0m), 6),
                    l.PrctjeDesc ?? 0m,
                    Math.Round(costo, 6)));
            }
            return resultado;
        }

        /// <summary>Línea de origen del movimiento (Ruling 4): la entrega o la orden de servicio base, o la propia factura.</summary>
        private static bool TryClaveOrigen(int? baseTipo, int? baseEntry, int? baseLinea, int entry, int noLinea,
            out (string TipoDoc, int DocEntry, int DocLinea) clave)
        {
            if (baseTipo == BaseEntrega || baseTipo == BaseOrdenServicio)
            {
                if (baseEntry is null || baseLinea is null)
                {
                    clave = default;
                    return false;
                }
                clave = (baseTipo == BaseEntrega ? TipoDocEntrega : TipoDocOrdenServicio, baseEntry.Value, baseLinea.Value);
                return true;
            }
            clave = (TipoDocFactura, entry, noLinea);
            return true;
        }
    }
}
