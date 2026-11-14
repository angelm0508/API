using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core.Reportes.Definiciones
{
    /// <summary>
    /// Precio neto pagado por un artículo en cada línea de factura o nota de débito de compra, por unidad base:
    /// <c>PrecioBase(Precio, FactorUom) × (1 − PrctjeDesc/100)</c>. Reporte de detalle: <c>.Take(limite)</c>, sin tope duro.
    /// </summary>
    public sealed class HistorialPreciosCompraReporte : IReporte
    {
        public const string CodigoReporte = "historial-precios-compra";

        private readonly IRepositorioGenerico<FacturaCompra, int> _facturas;
        private readonly IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> _detalles;

        public HistorialPreciosCompraReporte(
            IRepositorioGenerico<FacturaCompra, int> facturas,
            IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> detalles)
        {
            _facturas = facturas;
            _detalles = detalles;
        }

        public ReporteDefinicion Definicion { get; } = new(
            Codigo: CodigoReporte,
            Nombre: "Historial de precios de compra",
            Descripcion: "Precio neto pagado por un artículo a lo largo del tiempo.",
            Familia: "Compras",
            Modulo: "ReportesCompras",
            Filtros: new[]
            {
                new FiltroReporte("articulo", "Artículo", TipoFiltro.Articulo, Obligatorio: true),
                new FiltroReporte("fechas", "Fechas", TipoFiltro.RangoFechas),
                new FiltroReporte("proveedor", "Proveedor", TipoFiltro.Socio, SubTipo: "P")
            },
            Columnas: new[]
            {
                new ColumnaReporte("fecha", "Fecha", TipoColumna.Fecha),
                new ColumnaReporte("numDoc", "No.", TipoColumna.Entero),
                new ColumnaReporte("codigoSn", "Código proveedor", TipoColumna.Texto, VisiblePorDefecto: false),
                new ColumnaReporte("nombreSn", "Proveedor", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("articulo", "Artículo", TipoColumna.Texto),
                new ColumnaReporte("moneda", "Moneda", TipoColumna.Texto, VisiblePorDefecto: false),
                new ColumnaReporte("cantidad", "Cantidad (base)", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("precioNeto", "Precio neto por unidad base", TipoColumna.Dinero),
                new ColumnaReporte("descuento", "Descuento %", TipoColumna.Cantidad)
            });

        public async Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite)
        {
            var articulo = filtros.Texto("articulo");
            var desde = filtros.FechaDesde("fechas");
            var hasta = filtros.FechaHastaExclusiva("fechas");
            var proveedor = filtros.Texto("proveedor");

            // Facturas y notas de débito de compra válidas (subconsulta, no lista materializada).
            var validas = (await _facturas.ObtenerTodoAsync())
                .Where(f => f.Cancelado != "S" && (f.DocSubType == "--" || f.DocSubType == "DN"));
            if (desde is { } d) validas = validas.Where(f => f.FechaDoc >= d);
            if (hasta is { } h) validas = validas.Where(f => f.FechaDoc < h);
            if (proveedor is not null) validas = validas.Where(f => f.CodigoSn == proveedor);

            var detalles = (await _detalles.ObtenerTodoAsync()).Where(l => l.CodArticulo == articulo);

            var lineas = from l in detalles join f in validas on l.Entry equals f.Entry select new { l, f };

            var filas = await lineas
                .OrderBy(x => x.f.FechaDoc).ThenBy(x => x.l.Entry).ThenBy(x => x.l.NoLinea)
                .Select(x => new
                {
                    x.f.FechaDoc,
                    x.f.NumDoc,
                    x.f.CodigoSn,
                    x.f.NombreSn,
                    x.f.MonedaDoc,
                    x.l.CodArticulo,
                    x.l.Cantidad,
                    x.l.CantidadBase,
                    x.l.Precio,
                    x.l.FactorUom,
                    x.l.PrctjeDesc
                })
                .Take(limite)
                .ToListAsync();

            // El precio se calcula en memoria (solo las filas ya limitadas): UnidadesLinea no se traduce a SQL.
            return filas.Select(x =>
            {
                var descuento = x.PrctjeDesc ?? 0m;
                var precioBase = UnidadesLinea.PrecioBase(x.Precio ?? 0m, x.FactorUom);
                return new FilaReporte
                {
                    ["fecha"] = x.FechaDoc,
                    ["numDoc"] = x.NumDoc,
                    ["codigoSn"] = x.CodigoSn,
                    ["nombreSn"] = x.NombreSn,
                    ["articulo"] = x.CodArticulo,
                    ["moneda"] = x.MonedaDoc,
                    ["cantidad"] = x.CantidadBase ?? x.Cantidad ?? 0m,
                    ["precioNeto"] = Math.Round(precioBase * (1m - descuento / 100m), 6, MidpointRounding.AwayFromZero),
                    ["descuento"] = descuento
                };
            }).ToList();
        }
    }
}
