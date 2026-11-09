using System.Globalization;
using System.Text;
using API.Domain.Interface.Reportes;

namespace API.Infraestructure.Exportacion
{
    /// <summary>
    /// CSV para importar en otros sistemas (UTF-8 con BOM para que Excel lo abra bien). Solo datos: sin filas de total.
    /// Con agrupación agrega las columnas "Grupo 1" y "Grupo 2" y una fila por detalle (o por grupo hoja con sus subtotales si SoloResumen).
    /// </summary>
    public sealed class CsvExportador : IExportadorReporte
    {
        public string Formato => "csv";
        public string ContentType => "text/csv";
        public string Extension => "csv";

        public byte[] Exportar(ResultadoReporte resultado, ReporteDefinicion definicion, ContextoExportacion contexto)
        {
            var sb = new StringBuilder();
            var niveles = Profundidad(resultado.Grupos);

            var encabezados = new List<string>();
            for (var i = 1; i <= niveles; i++) encabezados.Add($"Grupo {i}");
            encabezados.AddRange(resultado.Columnas.Select(c => c.Titulo));
            Linea(sb, encabezados);

            if (resultado.Grupos.Count == 0)
            {
                foreach (var fila in resultado.Filas)
                    Linea(sb, resultado.Columnas.Select(c => Celda(c, fila.TryGetValue(c.Campo, out var v) ? v : null)));
            }
            else
            {
                Recorrer(resultado.Grupos, new List<string>(), niveles, resultado.Columnas, sb);
            }

            var preambulo = new UTF8Encoding(true).GetPreamble();
            var cuerpo = new UTF8Encoding(false).GetBytes(sb.ToString());
            return preambulo.Concat(cuerpo).ToArray();
        }

        private static void Recorrer(List<GrupoReporte> grupos, List<string> ruta, int niveles, IReadOnlyList<ColumnaReporte> columnas, StringBuilder sb)
        {
            foreach (var g in grupos)
            {
                var actual = new List<string>(ruta) { g.Etiqueta };
                if (g.Hijos.Count > 0)
                {
                    Recorrer(g.Hijos, actual, niveles, columnas, sb);
                    continue;
                }

                var prefijo = Enumerable.Range(0, niveles).Select(i => i < actual.Count ? actual[i] : "").ToList();
                if (g.Filas.Count > 0)
                {
                    foreach (var fila in g.Filas)
                        Linea(sb, prefijo.Concat(columnas.Select(c => Celda(c, fila.TryGetValue(c.Campo, out var v) ? v : null))));
                }
                else
                {
                    // SoloResumen: una fila por grupo hoja con sus subtotales (las columnas sin subtotal van vacías).
                    Linea(sb, prefijo.Concat(columnas.Select(c =>
                        c.Totaliza != Totalizacion.Ninguna && g.Subtotales.TryGetValue(c.Campo, out var s) ? Celda(c, s) : "")));
                }
            }
        }

        private static int Profundidad(List<GrupoReporte> grupos) =>
            grupos.Count == 0 ? 0 : 1 + grupos.Max(g => Profundidad(g.Hijos));

        private static string Celda(ColumnaReporte columna, object? valor)
        {
            if (valor is null) return "";
            if (valor is DateTime fecha) return fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            if (ComparadorNumerico.EsNumero(valor))
            {
                var n = Convert.ToDecimal(valor, CultureInfo.InvariantCulture);
                return columna.Tipo switch
                {
                    TipoColumna.Dinero => n.ToString("0.00####", CultureInfo.InvariantCulture),
                    TipoColumna.Entero => n.ToString("0", CultureInfo.InvariantCulture),
                    _ => n.ToString("0.######", CultureInfo.InvariantCulture)
                };
            }

            var texto = valor.ToString() ?? "";
            // Neutraliza fórmulas (=, +, -, @) en las columnas de texto: Excel las ejecutaría al abrir el archivo.
            if (columna.Tipo == TipoColumna.Texto && texto.Length > 0 && "=+-@".Contains(texto[0]))
                texto = "'" + texto;
            return texto;
        }

        private static void Linea(StringBuilder sb, IEnumerable<string> celdas)
        {
            sb.Append(string.Join(",", celdas.Select(Escapar)));
            sb.Append("\r\n");
        }

        private static string Escapar(string texto) =>
            texto.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + texto.Replace("\"", "\"\"") + "\"" : texto;
    }

    internal static class ComparadorNumerico
    {
        public static bool EsNumero(object valor) =>
            valor is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
    }
}
