using API.Domain.Core.Reportes.Compartido;
using API.Domain.Interface.Reportes;

namespace API.Domain.Core.Reportes.Definiciones
{
    /// <summary>
    /// Ranking de artículos por cantidad vendida, venta neta o utilidad. Una fila por (artículo, moneda): no mezcla monedas.
    /// Las líneas del período se agregan en memoria (tope de <see cref="ConsultaLineasVenta.MaxLineas"/> líneas).
    /// </summary>
    public sealed class ArticulosMasVendidosReporte : IReporte
    {
        public const string CodigoReporte = "articulos-mas-vendidos";

        private readonly ConsultaLineasVenta _consulta;

        public ArticulosMasVendidosReporte(ConsultaLineasVenta consulta)
        {
            _consulta = consulta;
        }

        public ReporteDefinicion Definicion { get; } = new(
            Codigo: CodigoReporte,
            Nombre: "Artículos más vendidos",
            Descripcion: "Ranking de artículos por cantidad vendida, venta neta o utilidad en el período.",
            Familia: "Ventas",
            Modulo: "ReportesVentas",
            Filtros: new[]
            {
                new FiltroReporte("fechas", "Fechas", TipoFiltro.RangoFechas, Obligatorio: true),
                new FiltroReporte("cliente", "Cliente", TipoFiltro.Socio, SubTipo: "C"),
                new FiltroReporte("grupo", "Grupo de artículos", TipoFiltro.Texto),
                new FiltroReporte("almacen", "Almacén", TipoFiltro.Almacen),
                new FiltroReporte("medirPor", "Medir por", TipoFiltro.Lista, Obligatorio: false, Opciones: new[]
                {
                    new OpcionFiltro("cantidad", "Cantidad vendida"),
                    new OpcionFiltro("venta", "Venta neta"),
                    new OpcionFiltro("utilidad", "Utilidad")
                }, ValorPorDefecto: "cantidad"),
                new FiltroReporte("primeros", "Mostrar los primeros", TipoFiltro.Numero)
            },
            Columnas: new[]
            {
                new ColumnaReporte("posicion", "#", TipoColumna.Entero),
                new ColumnaReporte("articulo", "Artículo", TipoColumna.Texto),
                new ColumnaReporte("nombreArticulo", "Nombre", TipoColumna.Texto),
                new ColumnaReporte("grupo", "Grupo", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("moneda", "Moneda", TipoColumna.Texto, VisiblePorDefecto: false),
                new ColumnaReporte("cantidad", "Cantidad (base)", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("ventaNeta", "Venta neta", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("costo", "Costo", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("utilidad", "Utilidad", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("documentos", "Facturas", TipoColumna.Entero, Totaliza: Totalizacion.Suma)
            });

        public async Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite)
        {
            var primeros = LeerPrimeros(filtros);
            var medirPor = filtros.Texto("medirPor") ?? "cantidad";

            var lineas = await _consulta.ObtenerAsync(new FiltroLineasVenta(
                filtros.FechaDesde("fechas"), filtros.FechaHastaExclusiva("fechas"),
                filtros.Texto("cliente"), null, filtros.Texto("almacen"), filtros.Texto("grupo")));

            var agregados = lineas
                .GroupBy(l => (l.CodArticulo, l.Moneda))
                .Select(g =>
                {
                    var primera = g.First();
                    var ventaNeta = g.Sum(l => l.VentaNeta);
                    var costo = g.Sum(l => l.Costo);
                    return new
                    {
                        Articulo = g.Key.CodArticulo,
                        Moneda = g.Key.Moneda,
                        primera.NombreArticulo,
                        primera.Grupo,
                        Cantidad = g.Sum(l => l.CantidadBase),
                        VentaNeta = ventaNeta,
                        Costo = costo,
                        Utilidad = ventaNeta - costo,
                        Documentos = g.Select(l => l.DocEntry).Distinct().Count()
                    };
                });

            var ordenados = medirPor switch
            {
                "venta" => agregados.OrderByDescending(a => a.VentaNeta),
                "utilidad" => agregados.OrderByDescending(a => a.Utilidad),
                _ => agregados.OrderByDescending(a => a.Cantidad)
            };

            var seleccion = ordenados
                .ThenBy(a => a.Articulo, StringComparer.Ordinal)
                .ThenBy(a => a.Moneda, StringComparer.Ordinal)
                .AsEnumerable();
            if (primeros is { } n) seleccion = seleccion.Take(n);

            // La posición se asigna después de recortar.
            return seleccion.Take(limite).Select((a, i) => new FilaReporte
            {
                ["posicion"] = i + 1,
                ["articulo"] = a.Articulo,
                ["nombreArticulo"] = a.NombreArticulo,
                ["grupo"] = a.Grupo,
                ["moneda"] = a.Moneda,
                ["cantidad"] = a.Cantidad,
                ["ventaNeta"] = a.VentaNeta,
                ["costo"] = a.Costo,
                ["utilidad"] = a.Utilidad,
                ["documentos"] = a.Documentos
            }).ToList();
        }

        private static int? LeerPrimeros(FiltrosReporte filtros)
        {
            if (filtros.Texto("primeros") is null) return null;
            var n = filtros.Numero("primeros");
            if (n is not { } valor || valor <= 0m || valor != decimal.Truncate(valor) || valor > int.MaxValue)
                throw new ReporteInvalidoException("El filtro 'Mostrar los primeros' debe ser un entero mayor que cero.");
            return (int)valor;
        }
    }
}
