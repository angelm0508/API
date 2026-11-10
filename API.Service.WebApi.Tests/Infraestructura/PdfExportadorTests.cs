using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Exportacion;
using API.Service.WebApi.Tests.TestHelpers;
using Xunit;

namespace API.Service.WebApi.Tests.Infraestructura
{
    public class PdfExportadorTests
    {
        private static readonly ReporteDefinicion Def = new("t", "Ventas por documento", "d", "Ventas", "ReportesVentas", Array.Empty<FiltroReporte>(), Array.Empty<ColumnaReporte>());

        // PNG mínimo válido de 1x1.
        private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

        private static ContextoExportacion Ctx(byte[]? grafico = null) =>
            new("Mi Empresa", "ana", new DateTime(2026, 11, 8, 10, 30, 0), "Sin filtros", grafico);

        private static ColumnaReporte Col(int i) => new($"c{i}", $"Columna {i}", i % 2 == 0 ? TipoColumna.Dinero : TipoColumna.Texto, Totaliza: i % 2 == 0 ? Totalizacion.Suma : Totalizacion.Ninguna);

        private static ResultadoReporte Plano(int columnas, int filas)
        {
            var r = new ResultadoReporte();
            for (var i = 0; i < columnas; i++) r.Columnas.Add(Col(i));
            for (var f = 0; f < filas; f++)
            {
                var fila = new FilaReporte();
                for (var i = 0; i < columnas; i++) fila[$"c{i}"] = i % 2 == 0 ? (object)(f + 0.5m) : $"Texto {f}";
                r.Filas.Add(fila);
            }
            r.Totales["c0"] = filas;
            return r;
        }

        private static bool EsPdf(byte[] bytes) => Encoding.ASCII.GetString(bytes, 0, 4) == "%PDF";

        [Fact]
        public void LosMetadatosSonLosDelPdf()
        {
            var e = new PdfExportador();

            Assert.Equal("pdf", e.Formato);
            Assert.Equal("application/pdf", e.ContentType);
            Assert.Equal("pdf", e.Extension);
        }

        [Fact]
        public void GeneraUnPdfValido()
        {
            var bytes = new PdfExportador().Exportar(Plano(4, 5), Def, Ctx());

            Assert.True(EsPdf(bytes));
            Assert.True(bytes.Length > 1000);
        }

        [Fact]
        public void ConMasDeDiezMilFilas_LanzaElMensajeDeTopeSinGenerarNada()
        {
            var r = Plano(4, 3);
            r.TotalFilas = 10001;

            var ex = Assert.Throws<ReporteInvalidoException>(() => new PdfExportador().Exportar(r, Def, Ctx()));

            Assert.Equal("El PDF admite hasta 10,000 filas; usa Excel o CSV o refina los filtros.", ex.Message);
        }

        [Fact]
        public void ConExactamenteDiezMilFilas_GeneraElPdf()
        {
            var r = Plano(4, 10000);
            r.TotalFilas = 10000;

            var bytes = new PdfExportador().Exportar(r, Def, Ctx());

            Assert.True(EsPdf(bytes));
        }

        [Fact]
        public void ConGraficoGeneraUnPdfMasGrandeQueSinEl()
        {
            var sin = new PdfExportador().Exportar(Plano(4, 5), Def, Ctx());
            var con = new PdfExportador().Exportar(Plano(4, 5), Def, Ctx(Png));

            Assert.True(EsPdf(con));
            Assert.True(con.Length > sin.Length);
        }

        private static (double Ancho, double Alto) TamanoPagina(byte[] bytes)
        {
            var texto = Encoding.Latin1.GetString(bytes);
            var m = Regex.Match(texto, @"/MediaBox\s*\[\s*([\d.]+)\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)\s*\]");
            Assert.True(m.Success, "No se encontró /MediaBox legible en el PDF.");
            var v = Enumerable.Range(1, 4).Select(i => double.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture)).ToArray();
            return (v[2] - v[0], v[3] - v[1]);
        }

        [Fact]
        public void ConSieteColumnas_LaPaginaEsVertical()
        {
            var (ancho, alto) = TamanoPagina(new PdfExportador().Exportar(Plano(7, 3), Def, Ctx()));

            Assert.True(ancho < alto, $"Se esperaba vertical y fue {ancho}x{alto}.");
        }

        [Fact]
        public void ConOchoColumnas_LaPaginaEsHorizontal()
        {
            var (ancho, alto) = TamanoPagina(new PdfExportador().Exportar(Plano(8, 3), Def, Ctx()));

            Assert.True(ancho > alto, $"Se esperaba horizontal y fue {ancho}x{alto}.");
        }

        [Fact]
        public void ConAgrupacionAvisosYTruncado_GeneraUnPdfValido()
        {
            var grupo = new GrupoReporte
            {
                Campo = "c1", Titulo = "Columna 1", Etiqueta = "Texto 0", Clave = "Texto 0", Conteo = 1,
                Subtotales = { ["c0"] = 0.5m },
                Filas = { ReporteFalso.Fila(("c0", 0.5m), ("c1", "Texto 0")) }
            };
            var r = new ResultadoReporte { Columnas = { Col(0), Col(1) }, Grupos = { grupo }, Totales = { ["c0"] = 0.5m }, Truncado = true };
            r.Avisos.Add("El resultado incluye más de una moneda: los importes no se suman.");

            var bytes = new PdfExportador().Exportar(r, Def, Ctx(Png));

            Assert.True(EsPdf(bytes));
        }

        [Fact]
        public void ConMuchasFilas_PaginaSinFallar()
        {
            var bytes = new PdfExportador().Exportar(Plano(5, 300), Def, Ctx());

            Assert.True(EsPdf(bytes));
        }

        [Fact]
        public void UnResultadoVacioGeneraUnPdfValido()
        {
            var bytes = new PdfExportador().Exportar(new ResultadoReporte { Columnas = { Col(0) } }, Def, Ctx());

            Assert.True(EsPdf(bytes));
        }
    }
}
