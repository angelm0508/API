namespace API.Domain.Interface.Reportes
{
    public enum TipoLinea { GrupoEncabezado, Detalle, Subtotal, Total }

    /// <summary>Una línea de la tabla de salida. Celdas está alineada con ResultadoReporte.Columnas (vacía en los encabezados de grupo).</summary>
    public sealed record LineaExportacion(TipoLinea Tipo, int Nivel, string Etiqueta, IReadOnlyList<object?> Celdas);

    /// <summary>Convierte un resultado (plano o agrupado) en una lista de líneas lista para pintar en Excel o PDF.</summary>
    public static class AplanadorResultado
    {
        public static List<LineaExportacion> Aplanar(ResultadoReporte resultado)
        {
            var lineas = new List<LineaExportacion>();

            if (resultado.Grupos.Count == 0)
            {
                foreach (var fila in resultado.Filas)
                    lineas.Add(Detalle(resultado.Columnas, fila, 0));
            }
            else
            {
                Recorrer(resultado.Columnas, resultado.Grupos, 0, lineas);
            }

            if (resultado.Totales.Count > 0)
                lineas.Add(new LineaExportacion(TipoLinea.Total, 0, "Total general", Valores(resultado.Columnas, resultado.Totales)));

            return lineas;
        }

        private static void Recorrer(IReadOnlyList<ColumnaReporte> columnas, List<GrupoReporte> grupos, int nivel, List<LineaExportacion> lineas)
        {
            foreach (var g in grupos)
            {
                var encabezado = $"{g.Titulo}: {g.Etiqueta} ({g.Conteo})";

                if (g.Hijos.Count == 0 && g.Filas.Count == 0)
                {
                    // SoloResumen: una sola línea por grupo hoja.
                    lineas.Add(new LineaExportacion(TipoLinea.Subtotal, nivel, encabezado, Valores(columnas, g.Subtotales)));
                    continue;
                }

                lineas.Add(new LineaExportacion(TipoLinea.GrupoEncabezado, nivel, encabezado, Array.Empty<object?>()));
                if (g.Hijos.Count > 0)
                    Recorrer(columnas, g.Hijos, nivel + 1, lineas);
                else
                    foreach (var fila in g.Filas)
                        lineas.Add(Detalle(columnas, fila, nivel + 1));
                lineas.Add(new LineaExportacion(TipoLinea.Subtotal, nivel, $"Subtotal {g.Etiqueta}", Valores(columnas, g.Subtotales)));
            }
        }

        private static LineaExportacion Detalle(IReadOnlyList<ColumnaReporte> columnas, FilaReporte fila, int nivel) =>
            new(TipoLinea.Detalle, nivel, "", columnas.Select(c => fila.TryGetValue(c.Campo, out var v) ? v : null).ToList());

        private static IReadOnlyList<object?> Valores(IReadOnlyList<ColumnaReporte> columnas, Dictionary<string, decimal?> valores) =>
            columnas.Select(c => valores.TryGetValue(c.Campo, out var v) ? (object?)v : null).ToList();
    }
}
