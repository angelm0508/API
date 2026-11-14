using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core.Reportes.Definiciones
{
    /// <summary>
    /// Artículos cuyo stock por almacén está bajo el mínimo o sobre el máximo configurado en <c>ArticuloAlmacen</c>.
    /// Parte de la configuración (solo filas con mínimo o máximo mayor que 0; el 0 es «sin nivel») y une la existencia
    /// por (artículo, almacén) con un left join en la misma consulta; sin fila de existencia el stock es 0.
    /// </summary>
    public sealed class StockBajoMinimoReporte : IReporte
    {
        public const string CodigoReporte = "stock-bajo-minimo";

        private readonly IRepositorioGenerico<ArticuloAlmacen, (string CodArticulo, string CodAlmacen)> _configuracion;
        private readonly IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)> _existencias;

        public StockBajoMinimoReporte(
            IRepositorioGenerico<ArticuloAlmacen, (string CodArticulo, string CodAlmacen)> configuracion,
            IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)> existencias)
        {
            _configuracion = configuracion;
            _existencias = existencias;
        }

        public ReporteDefinicion Definicion { get; } = new(
            Codigo: CodigoReporte,
            Nombre: "Stock bajo mínimo o sobre máximo",
            Descripcion: "Artículos cuyo stock por almacén está por debajo del mínimo o por encima del máximo configurado.",
            Familia: "Inventario",
            Modulo: "ReportesInventario",
            Filtros: new[]
            {
                new FiltroReporte("almacen", "Almacén", TipoFiltro.Almacen),
                new FiltroReporte("grupo", "Grupo de artículos", TipoFiltro.Texto),
                new FiltroReporte("estado", "Estado", TipoFiltro.Lista, Opciones: new[]
                {
                    new OpcionFiltro("FUERA", "Bajo mínimo o sobre máximo"),
                    new OpcionFiltro("BAJO", "Bajo mínimo"),
                    new OpcionFiltro("SOBRE", "Sobre máximo"),
                    new OpcionFiltro("TODOS", "Todos")
                }, ValorPorDefecto: "FUERA")
            },
            Columnas: new[]
            {
                new ColumnaReporte("almacen", "Almacén", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("articulo", "Artículo", TipoColumna.Texto),
                new ColumnaReporte("nombre", "Nombre", TipoColumna.Texto),
                new ColumnaReporte("grupo", "Grupo", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("enStock", "En stock", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("minimo", "Mínimo", TipoColumna.Cantidad),
                new ColumnaReporte("maximo", "Máximo", TipoColumna.Cantidad),
                new ColumnaReporte("estado", "Estado", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("nivelNecesario", "Por pedir", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma)
            });

        public async Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite)
        {
            var almacen = filtros.Texto("almacen");
            var grupo = filtros.Texto("grupo");
            var estado = filtros.Texto("estado") ?? "FUERA";

            var config = (await _configuracion.ObtenerTodoAsync()).Where(c => c.Minimo > 0 || c.Maximo > 0);
            if (almacen is not null) config = config.Where(c => c.CodAlmacen == almacen);
            if (grupo is not null)
                config = config.Where(c => c.CodArticuloNavigation.CodigoGrupoNavigation != null
                                        && c.CodArticuloNavigation.CodigoGrupoNavigation.Nombre!.Contains(grupo));

            var existencias = await _existencias.ObtenerTodoAsync();

            // Left join por (artículo, almacén): una sola consulta, sin lecturas por fila.
            var q = from c in config
                    join e in existencias on new { c.CodArticulo, c.CodAlmacen } equals new { e.CodArticulo, e.CodAlmacen } into porExistencia
                    from e in porExistencia.DefaultIfEmpty()
                    select new
                    {
                        c.CodAlmacen,
                        c.CodArticulo,
                        Nombre = c.CodArticuloNavigation.Nombre,
                        Grupo = c.CodArticuloNavigation.CodigoGrupoNavigation == null ? null : c.CodArticuloNavigation.CodigoGrupoNavigation.Nombre,
                        c.Minimo,
                        c.Maximo,
                        EnStock = e == null ? 0m : e.Disponible,
                        Comprometido = e == null ? 0m : e.Comprometido,
                        Pedido = e == null ? 0m : e.Pedido
                    };

            // El estado replica StockAlmacenCalculadora.Estado (el bajo mínimo tiene prioridad sobre el sobre máximo).
            switch (estado)
            {
                case "BAJO":
                    q = q.Where(x => x.Minimo > 0 && x.EnStock < x.Minimo);
                    break;
                case "SOBRE":
                    q = q.Where(x => !(x.Minimo > 0 && x.EnStock < x.Minimo) && x.Maximo > 0 && x.EnStock > x.Maximo);
                    break;
                case "FUERA":
                    q = q.Where(x => (x.Minimo > 0 && x.EnStock < x.Minimo) || (x.Maximo > 0 && x.EnStock > x.Maximo));
                    break;
            }

            var filas = await q.OrderBy(x => x.CodAlmacen).ThenBy(x => x.CodArticulo).Take(limite).ToListAsync();

            return filas.Select(x =>
            {
                var codigoEstado = StockAlmacenCalculadora.Estado(x.EnStock, x.Minimo, x.Maximo);
                var disponible = StockAlmacenCalculadora.Disponible(x.EnStock, x.Comprometido, x.Pedido);
                return new FilaReporte
                {
                    ["almacen"] = x.CodAlmacen,
                    ["articulo"] = x.CodArticulo,
                    ["nombre"] = x.Nombre,
                    ["grupo"] = x.Grupo,
                    ["enStock"] = x.EnStock,
                    ["minimo"] = x.Minimo ?? 0m,
                    ["maximo"] = x.Maximo ?? 0m,
                    ["estado"] = codigoEstado switch
                    {
                        StockAlmacenCalculadora.BajoMinimo => "Bajo mínimo",
                        StockAlmacenCalculadora.SobreMaximo => "Sobre máximo",
                        _ => "Normal"
                    },
                    ["nivelNecesario"] = StockAlmacenCalculadora.NivelNecesario(x.Maximo, disponible) ?? 0m
                };
            }).ToList();
        }
    }
}
