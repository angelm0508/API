using System.Globalization;
using API.Domain.Interface.Reportes;
using ClosedXML.Excel;

namespace API.Infraestructure.Exportacion
{
    /// <summary>
    /// Excel con encabezado (empresa, reporte, filtros, usuario y fecha), formato por tipo de columna, grupos con subtotales en
    /// negrita, fila de total, encabezado fijo y autofiltro cuando no hay agrupación.
    /// </summary>
    public sealed class ExcelExportador : IExportadorReporte
    {
        private const int FilaEncabezado = 6;

        public string Formato => "xlsx";
        public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        public string Extension => "xlsx";

        public byte[] Exportar(ResultadoReporte resultado, ReporteDefinicion definicion, ContextoExportacion contexto)
        {
            using var libro = new XLWorkbook();
            var hoja = libro.Worksheets.Add(NombreHoja(definicion.Nombre));

            var lineas = AplanadorResultado.Aplanar(resultado);
            // Una primera columna de etiquetas solo cuando hace falta (grupos, subtotales o total general).
            var conEtiqueta = resultado.Grupos.Count > 0 || resultado.Totales.Count > 0;
            var desplazamiento = conEtiqueta ? 1 : 0;
            var totalColumnas = Math.Max(1, resultado.Columnas.Count + desplazamiento);

            hoja.Cell(1, 1).Value = contexto.Empresa;
            hoja.Cell(1, 1).Style.Font.Bold = true;
            hoja.Cell(1, 1).Style.Font.FontSize = 14;
            hoja.Cell(2, 1).Value = definicion.Nombre;
            hoja.Cell(2, 1).Style.Font.Bold = true;
            hoja.Cell(3, 1).Value = contexto.FiltrosTexto;
            hoja.Cell(4, 1).Value = $"Generado por {contexto.Usuario} el {contexto.Fecha:yyyy-MM-dd HH:mm}";

            for (var i = 0; i < resultado.Columnas.Count; i++)
            {
                var c = hoja.Cell(FilaEncabezado, i + 1 + desplazamiento);
                c.Value = resultado.Columnas[i].Titulo;
                c.Style.Font.Bold = true;
                c.Style.Fill.BackgroundColor = XLColor.LightGray;
                c.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                if (EsNumerica(resultado.Columnas[i])) c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            }
            if (conEtiqueta)
            {
                var c = hoja.Cell(FilaEncabezado, 1);
                c.Style.Fill.BackgroundColor = XLColor.LightGray;
                c.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            }

            var fila = FilaEncabezado + 1;
            var ultimaDetalle = FilaEncabezado;
            foreach (var l in lineas)
            {
                switch (l.Tipo)
                {
                    case TipoLinea.GrupoEncabezado:
                        var g = hoja.Cell(fila, 1);
                        g.Value = l.Etiqueta;
                        g.Style.Font.Bold = true;
                        g.Style.Alignment.Indent = l.Nivel;
                        break;

                    case TipoLinea.Detalle:
                        EscribirCeldas(hoja, fila, desplazamiento, resultado.Columnas, l.Celdas);
                        ultimaDetalle = fila;
                        break;

                    case TipoLinea.Subtotal:
                    case TipoLinea.Total:
                        var etiqueta = hoja.Cell(fila, 1);
                        etiqueta.Value = l.Etiqueta;
                        etiqueta.Style.Alignment.Indent = l.Nivel;
                        EscribirCeldas(hoja, fila, desplazamiento, resultado.Columnas, l.Celdas);
                        var rango = hoja.Range(fila, 1, fila, totalColumnas);
                        rango.Style.Font.Bold = true;
                        rango.Style.Fill.BackgroundColor = l.Tipo == TipoLinea.Total ? XLColor.LightGray : XLColor.WhiteSmoke;
                        if (l.Tipo == TipoLinea.Total) rango.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                        break;
                }
                fila++;
            }

            hoja.SheetView.FreezeRows(FilaEncabezado);
            if (resultado.Grupos.Count == 0 && ultimaDetalle > FilaEncabezado)
                hoja.Range(FilaEncabezado, 1, ultimaDetalle, totalColumnas).SetAutoFilter();

            hoja.Columns().AdjustToContents(FilaEncabezado, Math.Max(fila, FilaEncabezado + 1));

            using var salida = new MemoryStream();
            libro.SaveAs(salida);
            return salida.ToArray();
        }

        private static void EscribirCeldas(IXLWorksheet hoja, int fila, int desplazamiento, IReadOnlyList<ColumnaReporte> columnas, IReadOnlyList<object?> celdas)
        {
            for (var i = 0; i < columnas.Count && i < celdas.Count; i++)
            {
                var celda = hoja.Cell(fila, i + 1 + desplazamiento);
                celda.Value = ACelda(celdas[i]);
                celda.Style.NumberFormat.Format = columnas[i].Tipo switch
                {
                    TipoColumna.Fecha => "yyyy-mm-dd",
                    TipoColumna.Dinero => "#,##0.00",
                    TipoColumna.Cantidad => "#,##0.######",
                    TipoColumna.Entero => "0",
                    _ => "General"
                };
            }
        }

        private static XLCellValue ACelda(object? valor)
        {
            if (valor is null) return Blank.Value;
            if (valor is DateTime d) return d;
            if (valor is string s) return s;
            if (valor is decimal m) return m;
            if (valor is int i) return i;
            if (valor is long l) return l;
            if (valor is double db) return db;
            return Convert.ToString(valor, CultureInfo.InvariantCulture) ?? "";
        }

        private static bool EsNumerica(ColumnaReporte c) => c.Tipo is TipoColumna.Dinero or TipoColumna.Cantidad or TipoColumna.Entero;

        private static string NombreHoja(string nombre)
        {
            var limpio = new string(nombre.Where(ch => !"[]:*?/\\".Contains(ch)).ToArray()).Trim();
            if (limpio.Length == 0) limpio = "Reporte";
            return limpio.Length > 31 ? limpio[..31] : limpio;
        }
    }
}
