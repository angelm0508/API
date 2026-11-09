using System.Text;
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
        public void ConGraficoGeneraUnPdfMasGrandeQueSinEl()
        {
            var sin = new PdfExportador().Exportar(Plano(4, 5), Def, Ctx());
            var con = new PdfExportador().Exportar(Plano(4, 5), Def, Ctx(Png));

            Assert.True(EsPdf(con));
            Assert.True(con.Length > sin.Length);
        }

        [Fact]
        public void MasDeSieteColumnas_GeneraElPdfEnHorizontal()
        {
            var bytes = new PdfExportador().Exportar(Plano(9, 3), Def, Ctx());

            Assert.True(EsPdf(bytes));
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
