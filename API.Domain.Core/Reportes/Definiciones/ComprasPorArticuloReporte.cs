using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core.Reportes.Definiciones
{
    /// <summary>
    /// Detalle de compras por línea de factura o nota de débito de compra: cantidad en unidad base y compra neta (sin IVA).
    /// Reporte de detalle: se limita con <c>.Take(limite)</c>, sin tope duro.
    /// </summary>
    public sealed class ComprasPorArticuloReporte : IReporte
    {
        public const string CodigoReporte = "compras-por-articulo";

        private readonly IRepositorioGenerico<FacturaCompra, int> _facturas;
        private readonly IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> _detalles;

        public ComprasPorArticuloReporte(
            IRepositorioGenerico<FacturaCompra, int> facturas,
            IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> detalles)
        {
            _facturas = facturas;
            _detalles = detalles;
        }

        public ReporteDefinicion Definicion { get; } = new(
            Codigo: CodigoReporte,
            Nombre: "Compras por artículo",
            Descripcion: "Cada línea comprada con su cantidad y compra neta; agrupa por artículo o proveedor para ver totales.",
            Familia: "Compras",
            Modulo: "ReportesCompras",
            Filtros: new[]
            {
                new FiltroReporte("fechas", "Fechas", TipoFiltro.RangoFechas),
                new FiltroReporte("proveedor", "Proveedor", TipoFiltro.Socio, SubTipo: "P"),
                new FiltroReporte("articulo", "Artículo", TipoFiltro.Articulo),
                new FiltroReporte("grupo", "Grupo de artículos", TipoFiltro.Texto),
                new FiltroReporte("almacen", "Almacén", TipoFiltro.Almacen)
            },
            Columnas: new[]
            {
                new ColumnaReporte("fecha", "Fecha", TipoColumna.Fecha, Agrupable: true),
                new ColumnaReporte("tipo", "Tipo", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("numDoc", "No.", TipoColumna.Entero),
                new ColumnaReporte("codigoSn", "Código proveedor", TipoColumna.Texto, Agrupable: true, VisiblePorDefecto: false),
                new ColumnaReporte("nombreSn", "Proveedor", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("articulo", "Artículo", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("nombreArticulo", "Nombre", TipoColumna.Texto),
                new ColumnaReporte("grupo", "Grupo", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("almacen", "Almacén", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("moneda", "Moneda", TipoColumna.Texto, Agrupable: true, VisiblePorDefecto: false),
                new ColumnaReporte("cantidad", "Cantidad (base)", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("precioNeto", "Precio neto", TipoColumna.Dinero),
                new ColumnaReporte("descuento", "Descuento %", TipoColumna.Cantidad),
                new ColumnaReporte("compraNeta", "Compra neta", TipoColumna.Dinero, Totaliza: Totalizacion.Suma)
            });

        public async Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite)
        {
            var desde = filtros.FechaDesde("fechas");
            var hasta = filtros.FechaHastaExclusiva("fechas");
            var proveedor = filtros.Texto("proveedor");
            var articulo = filtros.Texto("articulo");
            var almacen = filtros.Texto("almacen");
            var grupo = filtros.Texto("grupo");

            // Facturas y notas de débito de compra válidas (subconsulta, no lista materializada).
            var validas = (await _facturas.ObtenerTodoAsync())
                .Where(f => f.Cancelado != "S" && (f.DocSubType == "--" || f.DocSubType == "DN"));
            if (desde is { } d) validas = validas.Where(f => f.FechaDoc >= d);
            if (hasta is { } h) validas = validas.Where(f => f.FechaDoc < h);
            if (proveedor is not null) validas = validas.Where(f => f.CodigoSn == proveedor);

            var detalles = (await _detalles.ObtenerTodoAsync()).Where(l => l.CodArticulo != null);
            if (articulo is not null) detalles = detalles.Where(l => l.CodArticulo == articulo);
            if (almacen is not null) detalles = detalles.Where(l => l.CodAlmacen == almacen);
            if (grupo is not null)
                detalles = detalles.Where(l => l.CodArticuloNavigation != null
                                            && l.CodArticuloNavigation.CodigoGrupoNavigation != null
                                            && l.CodArticuloNavigation.CodigoGrupoNavigation.Nombre!.Contains(grupo));

            // El join con las facturas válidas (en SQL equivale a la subconsulta por Entry) evita materializar listas.
            var lineas = from l in detalles join f in validas on l.Entry equals f.Entry select new { l, f };

            var filas = await lineas
                .OrderBy(x => x.f.FechaDoc).ThenBy(x => x.l.Entry).ThenBy(x => x.l.NoLinea)
                .Select(x => new
                {
                    x.f.FechaDoc,
                    x.f.DocSubType,
                    x.f.NumDoc,
                    x.f.CodigoSn,
                    x.f.NombreSn,
                    x.f.MonedaDoc,
                    x.l.CodArticulo,
                    NombreArticulo = x.l.CodArticuloNavigation != null ? x.l.CodArticuloNavigation.Nombre : null,
                    Grupo = x.l.CodArticuloNavigation != null && x.l.CodArticuloNavigation.CodigoGrupoNavigation != null
                        ? x.l.CodArticuloNavigation.CodigoGrupoNavigation.Nombre : null,
                    x.l.CodAlmacen,
                    x.l.Cantidad,
                    x.l.CantidadBase,
                    x.l.TotalLinea,
                    x.l.Impuesto,
                    x.l.PrctjeDesc
                })
                .Take(limite)
                .ToListAsync();

            return filas.Select(x =>
            {
                var cantidadBase = x.CantidadBase ?? x.Cantidad ?? 0m;
                var compraNeta = Math.Round((x.TotalLinea ?? 0m) - (x.Impuesto ?? 0m), 6);
                return new FilaReporte
                {
                    ["fecha"] = x.FechaDoc,
                    ["tipo"] = x.DocSubType == "DN" ? "Nota de débito de compra" : "Factura de compra",
                    ["numDoc"] = x.NumDoc,
                    ["codigoSn"] = x.CodigoSn,
                    ["nombreSn"] = x.NombreSn,
                    ["articulo"] = x.CodArticulo,
                    ["nombreArticulo"] = x.NombreArticulo,
                    ["grupo"] = x.Grupo,
                    ["almacen"] = x.CodAlmacen,
                    ["moneda"] = x.MonedaDoc,
                    ["cantidad"] = cantidadBase,
                    ["precioNeto"] = cantidadBase == 0m ? 0m : Math.Round(compraNeta / cantidadBase, 6),
                    ["descuento"] = x.PrctjeDesc ?? 0m,
                    ["compraNeta"] = compraNeta
                };
            }).ToList();
        }
    }
}
