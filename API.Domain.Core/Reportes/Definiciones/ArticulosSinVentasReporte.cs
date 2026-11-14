using API.Domain.Core.Reportes.Compartido;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core.Reportes.Definiciones
{
    /// <summary>Artículos activos de venta que no aparecen en ninguna factura (o nota de débito) del período.</summary>
    public sealed class ArticulosSinVentasReporte : IReporte
    {
        public const string CodigoReporte = "articulos-sin-ventas";

        private readonly IRepositorioGenerico<Articulo, string> _articulos;
        private readonly ConsultaLineasVenta _consulta;

        public ArticulosSinVentasReporte(IRepositorioGenerico<Articulo, string> articulos, ConsultaLineasVenta consulta)
        {
            _articulos = articulos;
            _consulta = consulta;
        }

        public ReporteDefinicion Definicion { get; } = new(
            Codigo: CodigoReporte,
            Nombre: "Artículos sin ventas",
            Descripcion: "Artículos activos de venta que no se vendieron en el período.",
            Familia: "Ventas",
            Modulo: "ReportesVentas",
            Filtros: new[]
            {
                new FiltroReporte("fechas", "Fechas", TipoFiltro.RangoFechas, Obligatorio: true),
                new FiltroReporte("grupo", "Grupo de artículos", TipoFiltro.Texto)
            },
            Columnas: new[]
            {
                new ColumnaReporte("articulo", "Artículo", TipoColumna.Texto),
                new ColumnaReporte("nombre", "Nombre", TipoColumna.Texto),
                new ColumnaReporte("grupo", "Grupo", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("existencias", "En stock", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("costoUnitario", "Costo promedio", TipoColumna.Dinero),
                new ColumnaReporte("valorInventario", "Valor del inventario", TipoColumna.Dinero, Totaliza: Totalizacion.Suma)
            });

        public async Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite)
        {
            var grupo = filtros.Texto("grupo");

            var vendidas = await _consulta.ObtenerAsync(new FiltroLineasVenta(
                filtros.FechaDesde("fechas"), filtros.FechaHastaExclusiva("fechas"), null, null, null, null));
            var codigosVendidos = vendidas.Select(l => l.CodArticulo).ToHashSet(StringComparer.Ordinal);

            var q = (await _articulos.ObtenerTodoAsync()).Where(a => a.Activo == "S" && a.ArticuloVenta == "S");
            if (grupo is not null)
                q = q.Where(a => a.CodigoGrupoNavigation != null && a.CodigoGrupoNavigation.Nombre!.Contains(grupo));

            // Los artículos vendidos se descartan en memoria: una cláusula IN con miles de códigos no es viable en SQL Server.
            var candidatos = await q
                .Select(a => new
                {
                    a.Codigo,
                    a.Nombre,
                    Grupo = a.CodigoGrupoNavigation == null ? null : a.CodigoGrupoNavigation.Nombre,
                    a.CantDisponible,
                    a.CostoPromedio,
                    a.ValorInventario
                })
                .ToListAsync();

            return candidatos
                .Where(a => !codigosVendidos.Contains(a.Codigo))
                .OrderByDescending(a => a.ValorInventario)
                .ThenBy(a => a.Codigo, StringComparer.Ordinal)
                .Take(limite)
                .Select(a => new FilaReporte
                {
                    ["articulo"] = a.Codigo,
                    ["nombre"] = a.Nombre,
                    ["grupo"] = a.Grupo,
                    ["existencias"] = a.CantDisponible ?? 0m,
                    ["costoUnitario"] = a.CostoPromedio,
                    ["valorInventario"] = a.ValorInventario
                })
                .ToList();
        }
    }
}
