using System.Text;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Exportacion;
using API.Service.WebApi.Tests.TestHelpers;
using Xunit;

namespace API.Service.WebApi.Tests.Infraestructura
{
    public class CsvExportadorTests
    {
        private static readonly ReporteDefinicion Def = new("t", "Prueba", "d", "F", "ReportesVentas", Array.Empty<FiltroReporte>(), Array.Empty<ColumnaReporte>());
        private static readonly ContextoExportacion Ctx = new("Empresa", "tester", new DateTime(2026, 11, 8, 10, 30, 0), "Sin filtros", null);

        private static readonly ColumnaReporte CFecha = new("fecha", "Fecha", TipoColumna.Fecha);
        private static readonly ColumnaReporte CTexto = new("cliente", "Cliente", TipoColumna.Texto);
        private static readonly ColumnaReporte CCant = new("cantidad", "Cantidad", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma);
        private static readonly ColumnaReporte CDinero = new("importe", "Importe", TipoColumna.Dinero, Totaliza: Totalizacion.Suma);

        private static (byte[] Bytes, string[] Lineas) Generar(ResultadoReporte r)
        {
            var bytes = new CsvExportador().Exportar(r, Def, Ctx);
            var texto = new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
            return (bytes, texto.Split("\r\n", StringSplitOptions.RemoveEmptyEntries));
        }

        private static FilaReporte Fila(DateTime fecha, string cliente, decimal cantidad, decimal importe) =>
            ReporteFalso.Fila(("fecha", fecha), ("cliente", cliente), ("cantidad", cantidad), ("importe", importe));

        [Fact]
        public void LosMetadatosSonLosDelCsv()
        {
            var e = new CsvExportador();

            Assert.Equal("csv", e.Formato);
            Assert.Equal("text/csv", e.ContentType);
            Assert.Equal("csv", e.Extension);
        }

        [Fact]
        public void EmpiezaConBomUtf8YLlevaLosTitulosComoEncabezado()
        {
            var r = new ResultadoReporte { Columnas = { CFecha, CTexto, CCant, CDinero } };

            var (bytes, lineas) = Generar(r);

            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
            Assert.Equal("Fecha,Cliente,Cantidad,Importe", lineas[0]);
        }

        [Fact]
        public void FormateaFechasYNumerosConPuntoDecimalSinTotales()
        {
            var r = new ResultadoReporte
            {
                Columnas = { CFecha, CTexto, CCant, CDinero },
                Filas = { Fila(new DateTime(2026, 1, 5), "C1", 2.5m, 1234.5m), Fila(new DateTime(2026, 1, 6), "C2", 3m, 0.123456m) },
                Totales = { ["cantidad"] = 5.5m, ["importe"] = 1234.623456m }
            };

            var (_, lineas) = Generar(r);

            Assert.Equal(3, lineas.Length);   // encabezado + 2 filas: sin fila de total
            Assert.Equal("2026-01-05,C1,2.5,1234.50", lineas[1]);
            Assert.Equal("2026-01-06,C2,3,0.123456", lineas[2]);
        }

        [Fact]
        public void EscapaComasComillasYSaltosDeLinea()
        {
            var r = new ResultadoReporte
            {
                Columnas = { CTexto },
                Filas = { ReporteFalso.Fila(("cliente", "Pérez, \"Juan\"\nJr.")) }
            };

            var bytes = new CsvExportador().Exportar(r, Def, Ctx);
            var texto = new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);

            Assert.Contains("\"Pérez, \"\"Juan\"\"\nJr.\"", texto);
        }

        [Fact]
        public void NeutralizaLasFormulasEnLasColumnasDeTexto()
        {
            var r = new ResultadoReporte
            {
                Columnas = { CTexto },
                Filas = { ReporteFalso.Fila(("cliente", "=HYPERLINK(\"x\")")), ReporteFalso.Fila(("cliente", "@cmd")), ReporteFalso.Fila(("cliente", "-5 cajas")) }
            };

            var (_, lineas) = Generar(r);

            Assert.Equal("\"'=HYPERLINK(\"\"x\"\")\"", lineas[1]);
            Assert.Equal("'@cmd", lineas[2]);
            Assert.Equal("'-5 cajas", lineas[3]);
        }

        [Fact]
        public void LosNumerosNegativosNoSeTocan()
        {
            var r = new ResultadoReporte { Columnas = { CDinero }, Filas = { ReporteFalso.Fila(("importe", -56m)) } };

            var (_, lineas) = Generar(r);

            Assert.Equal("-56.00", lineas[1]);
        }

        [Fact]
        public void ConAgrupacion_AgregaColumnasGrupoYUnaFilaPorDetalle()
        {
            var hoja1 = new GrupoReporte { Campo = "cliente", Titulo = "Cliente", Etiqueta = "A", Clave = "A", Conteo = 2, Filas = { Fila(new DateTime(2026, 1, 5), "A", 1m, 10m), Fila(new DateTime(2026, 1, 6), "A", 2m, 20m) } };
            var mes1 = new GrupoReporte { Campo = "fecha", Titulo = "Fecha", Etiqueta = "2026-01", Clave = "2026-01", Conteo = 2, Filas = hoja1.Filas };
            var raiz = new GrupoReporte { Campo = "cliente", Titulo = "Cliente", Etiqueta = "A", Clave = "A", Conteo = 2, Hijos = { mes1 } };
            var r = new ResultadoReporte { Columnas = { CFecha, CCant, CDinero }, Grupos = { raiz } };

            var (_, lineas) = Generar(r);

            Assert.Equal("Grupo 1,Grupo 2,Fecha,Cantidad,Importe", lineas[0]);
            Assert.Equal("A,2026-01,2026-01-05,1,10.00", lineas[1]);
            Assert.Equal("A,2026-01,2026-01-06,2,20.00", lineas[2]);
            Assert.Equal(3, lineas.Length);
        }

        [Fact]
        public void ConAgrupacion_NeutralizaLasFormulasEnLasEtiquetasDeGrupo()
        {
            var hoja1 = new GrupoReporte { Campo = "cliente", Titulo = "Cliente", Etiqueta = "=HYPERLINK(\"x\")", Clave = "k1", Conteo = 1, Subtotales = { ["cantidad"] = 1m } };
            var hoja2 = new GrupoReporte { Campo = "cliente", Titulo = "Cliente", Etiqueta = "+34 cajas", Clave = "k2", Conteo = 1, Subtotales = { ["cantidad"] = 2m } };
            var r = new ResultadoReporte { Columnas = { CCant }, Grupos = { hoja1, hoja2 } };

            var (_, lineas) = Generar(r);

            Assert.Equal("\"'=HYPERLINK(\"\"x\"\")\",1", lineas[1]);
            Assert.Equal("'+34 cajas,2", lineas[2]);
        }

        [Fact]
        public void ConAgrupacionDeDosNiveles_NeutralizaLasEtiquetasDeAmbasColumnasGrupo()
        {
            var hoja = new GrupoReporte
            {
                Campo = "fecha", Titulo = "Fecha", Etiqueta = "=HYPERLINK(\"x\")", Clave = "h", Conteo = 1,
                Filas = { Fila(new DateTime(2026, 1, 5), "A", 1m, 10m) }
            };
            var raiz = new GrupoReporte { Campo = "cliente", Titulo = "Cliente", Etiqueta = "+1", Clave = "r", Conteo = 1, Hijos = { hoja } };
            var r = new ResultadoReporte { Columnas = { CFecha, CCant, CDinero }, Grupos = { raiz } };

            var (_, lineas) = Generar(r);

            Assert.Equal("Grupo 1,Grupo 2,Fecha,Cantidad,Importe", lineas[0]);
            Assert.Equal("'+1,\"'=HYPERLINK(\"\"x\"\")\",2026-01-05,1,10.00", lineas[1]);
        }

        [Fact]
        public void ConAgrupacionYSoloResumen_UnaFilaPorGrupoHojaConSusSubtotales()
        {
            var hoja = new GrupoReporte
            {
                Campo = "cliente", Titulo = "Cliente", Etiqueta = "A", Clave = "A", Conteo = 2,
                Subtotales = { ["cantidad"] = 3m, ["importe"] = 30m }
            };
            var r = new ResultadoReporte { Columnas = { CFecha, CCant, CDinero }, Grupos = { hoja } };

            var (_, lineas) = Generar(r);

            Assert.Equal("Grupo 1,Fecha,Cantidad,Importe", lineas[0]);
            Assert.Equal("A,,3,30.00", lineas[1]);
        }
    }
}
