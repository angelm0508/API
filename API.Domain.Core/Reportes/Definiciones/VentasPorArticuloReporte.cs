using API.Domain.Core.Reportes.Compartido;
using API.Domain.Interface.Reportes;

namespace API.Domain.Core.Reportes.Definiciones
{
    /// <summary>Detalle de ventas por línea de factura o nota de débito: venta neta (sin IVA), costo y utilidad.</summary>
    public sealed class VentasPorArticuloReporte : IReporte
    {
        public const string CodigoReporte = "ventas-por-articulo";

        private readonly ConsultaLineasVenta _consulta;

        public VentasPorArticuloReporte(ConsultaLineasVenta consulta)
        {
            _consulta = consulta;
        }

        public ReporteDefinicion Definicion { get; } = new(
            Codigo: CodigoReporte,
            Nombre: "Ventas por artículo",
            Descripcion: "Cada línea vendida con su venta neta, costo y utilidad.",
            Familia: "Ventas",
            Modulo: "ReportesVentas",
            Filtros: new[]
            {
                new FiltroReporte("fechas", "Fechas", TipoFiltro.RangoFechas),
                new FiltroReporte("cliente", "Cliente", TipoFiltro.Socio, SubTipo: "C"),
                new FiltroReporte("articulo", "Artículo", TipoFiltro.Articulo),
                new FiltroReporte("grupo", "Grupo de artículos", TipoFiltro.Texto),
                new FiltroReporte("almacen", "Almacén", TipoFiltro.Almacen)
            },
            Columnas: new[]
            {
                new ColumnaReporte("fecha", "Fecha", TipoColumna.Fecha, Agrupable: true),
                new ColumnaReporte("tipo", "Tipo", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("serie", "Serie", TipoColumna.Entero, VisiblePorDefecto: false),
                new ColumnaReporte("numDoc", "No.", TipoColumna.Entero),
                new ColumnaReporte("codigoSn", "Código cliente", TipoColumna.Texto, Agrupable: true, VisiblePorDefecto: false),
                new ColumnaReporte("nombreSn", "Cliente", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("articulo", "Artículo", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("nombreArticulo", "Nombre", TipoColumna.Texto),
                new ColumnaReporte("grupo", "Grupo", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("almacen", "Almacén", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("moneda", "Moneda", TipoColumna.Texto, Agrupable: true, VisiblePorDefecto: false),
                new ColumnaReporte("cantidad", "Cantidad (base)", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("precioNeto", "Precio neto", TipoColumna.Dinero),
                new ColumnaReporte("ventaNeta", "Venta neta", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("costo", "Costo", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("utilidad", "Utilidad", TipoColumna.Dinero, Totaliza: Totalizacion.Suma)
            });

        public async Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite)
        {
            var lineas = await _consulta.ObtenerLimitadoAsync(new FiltroLineasVenta(
                filtros.FechaDesde("fechas"), filtros.FechaHastaExclusiva("fechas"),
                filtros.Texto("cliente"), filtros.Texto("articulo"), filtros.Texto("almacen"), filtros.Texto("grupo")), limite);

            // Detalle: el límite se aplica en la consulta (no hay tope de MaxLineas) y ya vienen en orden fecha, documento, línea.
            return lineas.Select(l => new FilaReporte
            {
                ["fecha"] = l.Fecha,
                ["tipo"] = l.Tipo,
                ["serie"] = l.Serie,
                ["numDoc"] = l.NumDoc,
                ["codigoSn"] = l.CodigoSn,
                ["nombreSn"] = l.NombreSn,
                ["articulo"] = l.CodArticulo,
                ["nombreArticulo"] = l.NombreArticulo,
                ["grupo"] = l.Grupo,
                ["almacen"] = l.CodAlmacen,
                ["moneda"] = l.Moneda,
                ["cantidad"] = l.CantidadBase,
                ["precioNeto"] = l.CantidadBase == 0m ? 0m : Math.Round(l.VentaNeta / l.CantidadBase, 6),
                ["ventaNeta"] = l.VentaNeta,
                ["costo"] = l.Costo,
                ["utilidad"] = l.VentaNeta - l.Costo
            }).ToList();
        }
    }
}
