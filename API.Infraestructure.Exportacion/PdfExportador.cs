using API.Domain.Interface.Reportes;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace API.Infraestructure.Exportacion
{
    /// <summary>
    /// PDF con encabezado (empresa, reporte, filtros, usuario y fecha), pie «Página X de Y», gráfico opcional sobre la tabla y la
    /// tabla del resultado (horizontal si hay más de 7 columnas). Licencia QuestPDF Community (gratuita para ingresos anuales menores a US$1 millón).
    /// </summary>
    public sealed class PdfExportador : IExportadorReporte
    {
        private const int MaxColumnasVertical = 7;
        private const int MaxFilasPdf = 10000;

        static PdfExportador()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public string Formato => "pdf";
        public string ContentType => "application/pdf";
        public string Extension => "pdf";

        public byte[] Exportar(ResultadoReporte resultado, ReporteDefinicion definicion, ContextoExportacion contexto)
        {
            if (resultado.TotalFilas > MaxFilasPdf)
                throw new ReporteInvalidoException("El PDF admite hasta 10,000 filas; usa Excel o CSV o refina los filtros.");

            var lineas = AplanadorResultado.Aplanar(resultado);
            var columnas = resultado.Columnas;
            var conEtiqueta = resultado.Grupos.Count > 0 || resultado.Totales.Count > 0;
            var totalColumnas = Math.Max(1, columnas.Count + (conEtiqueta ? 1 : 0));
            var horizontal = columnas.Count > MaxColumnasVertical;

            var documento = Document.Create(contenedor =>
            {
                contenedor.Page(pagina =>
                {
                    pagina.Size(horizontal ? PageSizes.A4.Landscape() : PageSizes.A4);
                    pagina.Margin(24);
                    pagina.DefaultTextStyle(x => x.FontSize(8));

                    pagina.Header().Column(col =>
                    {
                        col.Item().Text(contexto.Empresa).FontSize(14).Bold();
                        col.Item().Text(definicion.Nombre).FontSize(11).SemiBold();
                        col.Item().Text(contexto.FiltrosTexto).FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingBottom(6).Text($"Generado por {contexto.Usuario} el {contexto.Fecha:yyyy-MM-dd HH:mm}")
                            .FontColor(Colors.Grey.Darken2);
                    });

                    pagina.Content().Column(col =>
                    {
                        if (contexto.GraficoPng is { Length: > 0 })
                            col.Item().PaddingBottom(8).Height(220).Image(contexto.GraficoPng).FitArea();

                        foreach (var aviso in resultado.Avisos)
                            col.Item().PaddingBottom(2).Text(aviso).FontColor(Colors.Orange.Darken3);
                        if (resultado.Truncado)
                            col.Item().PaddingBottom(2).Text("El resultado se truncó al máximo de filas: refine los filtros para ver el resto.")
                                .FontColor(Colors.Orange.Darken3);

                        col.Item().Table(tabla =>
                        {
                            tabla.ColumnsDefinition(c =>
                            {
                                if (conEtiqueta) c.RelativeColumn(2);
                                foreach (var _ in columnas) c.RelativeColumn();
                            });

                            tabla.Header(h =>
                            {
                                if (conEtiqueta) h.Cell().Element(Cabecera).Text("");
                                foreach (var c in columnas)
                                {
                                    var celda = h.Cell().Element(Cabecera);
                                    (EsNumerica(c) ? celda.AlignRight() : celda).Text(c.Titulo);
                                }
                            });

                            foreach (var l in lineas)
                            {
                                if (l.Tipo == TipoLinea.GrupoEncabezado)
                                {
                                    tabla.Cell().ColumnSpan((uint)totalColumnas).Element(EstiloGrupo)
                                        .PaddingLeft(l.Nivel * 8).Text(l.Etiqueta).Bold();
                                    continue;
                                }

                                var resaltada = l.Tipo != TipoLinea.Detalle;
                                if (conEtiqueta)
                                {
                                    var etiqueta = tabla.Cell().Element(c => Celda(c, resaltada)).PaddingLeft(l.Nivel * 8);
                                    var texto = etiqueta.Text(l.Etiqueta);
                                    if (resaltada) texto.Bold();
                                }

                                for (var i = 0; i < columnas.Count; i++)
                                {
                                    var valor = i < l.Celdas.Count ? l.Celdas[i] : null;
                                    var celda = tabla.Cell().Element(c => Celda(c, resaltada));
                                    var texto = (EsNumerica(columnas[i]) ? celda.AlignRight() : celda).Text(FormatoValores.Texto(columnas[i], valor));
                                    if (resaltada) texto.Bold();
                                }
                            }
                        });
                    });

                    pagina.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Página ");
                        t.CurrentPageNumber();
                        t.Span(" de ");
                        t.TotalPages();
                    });
                });
            });

            return documento.GeneratePdf();
        }

        private static bool EsNumerica(ColumnaReporte c) => c.Tipo is TipoColumna.Dinero or TipoColumna.Cantidad or TipoColumna.Entero;

        private static IContainer Cabecera(IContainer c) =>
            c.Background(Colors.Grey.Lighten3).BorderBottom(0.5f).PaddingVertical(3).PaddingHorizontal(3).DefaultTextStyle(x => x.SemiBold());

        private static IContainer EstiloGrupo(IContainer c) =>
            c.Background(Colors.Grey.Lighten4).PaddingVertical(2).PaddingHorizontal(3);

        private static IContainer Celda(IContainer c, bool resaltada) =>
            (resaltada ? c.Background(Colors.Grey.Lighten4) : c)
                .BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingHorizontal(3);
    }
}
