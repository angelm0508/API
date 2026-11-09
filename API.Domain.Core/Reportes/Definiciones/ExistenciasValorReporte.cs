using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core.Reportes.Definiciones
{
    /// <summary>Existencias y valor de inventario por almacén. Valor = en stock × costo (promedio o estándar según el método de valuación).</summary>
    public sealed class ExistenciasValorReporte : IReporte
    {
        public const string CodigoReporte = "existencias-valor";

        private readonly IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)> _existencias;

        public ExistenciasValorReporte(IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)> existencias)
        {
            _existencias = existencias;
        }

        public ReporteDefinicion Definicion { get; } = new(
            Codigo: CodigoReporte,
            Nombre: "Existencias y valor por almacén",
            Descripcion: "Cantidad en stock, comprometida y pedida por artículo y almacén, con el valor del inventario.",
            Familia: "Inventario",
            Modulo: "ReportesInventario",
            Filtros: new[]
            {
                new FiltroReporte("almacen", "Almacén", TipoFiltro.Almacen),
                new FiltroReporte("articulo", "Artículo", TipoFiltro.Articulo),
                new FiltroReporte("grupo", "Grupo de artículos", TipoFiltro.Texto),
                new FiltroReporte("soloConExistencias", "Solo con existencias", TipoFiltro.Lista, Opciones: new[]
                {
                    new OpcionFiltro("N", "No (todas las filas)"),
                    new OpcionFiltro("S", "Sí (solo con stock)")
                }, ValorPorDefecto: "N")
            },
            Columnas: new[]
            {
                new ColumnaReporte("almacen", "Almacén", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("articulo", "Artículo", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("nombre", "Nombre", TipoColumna.Texto),
                new ColumnaReporte("grupo", "Grupo", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("enStock", "En stock", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("comprometido", "Comprometido", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma, VisiblePorDefecto: false),
                new ColumnaReporte("pedido", "Pedido", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma, VisiblePorDefecto: false),
                new ColumnaReporte("costo", "Costo unitario", TipoColumna.Dinero),
                new ColumnaReporte("valor", "Valor", TipoColumna.Dinero, Totaliza: Totalizacion.Suma)
            });

        public async Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite)
        {
            var almacen = filtros.Texto("almacen");
            var articulo = filtros.Texto("articulo");
            var grupo = filtros.Texto("grupo");
            var soloConExistencias = filtros.Texto("soloConExistencias") == "S";

            var q = await _existencias.ObtenerTodoAsync();
            if (almacen is not null) q = q.Where(e => e.CodAlmacen == almacen);
            if (articulo is not null) q = q.Where(e => e.CodArticulo == articulo);
            if (grupo is not null)
                q = q.Where(e => e.CodArticuloNavigation.CodigoGrupoNavigation != null
                              && e.CodArticuloNavigation.CodigoGrupoNavigation.Nombre!.Contains(grupo));
            if (soloConExistencias) q = q.Where(e => e.Disponible > 0);

            var filas = await q.OrderBy(e => e.CodAlmacen).ThenBy(e => e.CodArticulo).Take(limite)
                .Select(e => new
                {
                    e.CodAlmacen,
                    e.CodArticulo,
                    Nombre = e.CodArticuloNavigation.Nombre,
                    Grupo = e.CodArticuloNavigation.CodigoGrupoNavigation == null ? null : e.CodArticuloNavigation.CodigoGrupoNavigation.Nombre,
                    e.Disponible,
                    e.Comprometido,
                    e.Pedido,
                    Costo = e.CodArticuloNavigation.MetodoValuacion == "E"
                        ? e.CodArticuloNavigation.CostoEstandar
                        : e.CodArticuloNavigation.CostoPromedio
                })
                .ToListAsync();

            return filas.Select(x => new FilaReporte
            {
                ["almacen"] = x.CodAlmacen,
                ["articulo"] = x.CodArticulo,
                ["nombre"] = x.Nombre,
                ["grupo"] = x.Grupo,
                ["enStock"] = x.Disponible,
                ["comprometido"] = x.Comprometido,
                ["pedido"] = x.Pedido,
                ["costo"] = x.Costo,
                ["valor"] = Math.Round(x.Disponible * x.Costo, 6)
            }).ToList();
        }
    }
}
