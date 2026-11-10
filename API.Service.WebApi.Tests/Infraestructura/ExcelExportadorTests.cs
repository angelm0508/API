using API.Domain.Interface.Reportes;
using API.Infraestructure.Exportacion;
using API.Service.WebApi.Tests.TestHelpers;
using ClosedXML.Excel;
using Xunit;

namespace API.Service.WebApi.Tests.Infraestructura
{
    public class ExcelExportadorTests
    {
        private static readonly ReporteDefinicion Def = new("t", "Ventas por documento", "d", "Ventas", "ReportesVentas", Array.Empty<FiltroReporte>(), Array.Empty<ColumnaReporte>());
        private static readonly ContextoExportacion Ctx = new("Mi Empresa", "ana", new DateTime(2026, 11, 8, 10, 30, 0), "Fechas: 2026-01-01 a 2026-01-31", null);

        private static readonly ColumnaReporte CFecha = new("fecha", "Fecha", TipoColumna.Fecha);
        private static readonly ColumnaReporte CTexto = new("cliente", "Cliente", TipoColumna.Texto);
        private static readonly ColumnaReporte CCant = new("cantidad", "Cantidad", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma);
        private static readonly ColumnaReporte CImporte = new("importe", "Importe", TipoColumna.Dinero, Totaliza: Totalizacion.Suma);

        private static FilaReporte Fila(DateTime fecha, string cliente, decimal cant, decimal importe) =>
            ReporteFalso.Fila(("fecha", fecha), ("cliente", cliente), ("cantidad", cant), ("importe", importe));

        private static XLWorkbook Abrir(ResultadoReporte r) =>
            new(new MemoryStream(new ExcelExportador().Exportar(r, Def, Ctx)));

        [Fact]
        public void LosMetadatosSonLosDelXlsx()
        {
            var e = new ExcelExportador();

            Assert.Equal("xlsx", e.Formato);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", e.ContentType);
            Assert.Equal("xlsx", e.Extension);
        }

        [Fact]
        public void SinAgrupacion_EncabezadoDeEmpresaFiltrosTitulosYFormatos()
        {
            var r = new ResultadoReporte
            {
                Columnas = { CFecha, CTexto, CCant, CImporte },
                Filas = { Fila(new DateTime(2026, 1, 5), "C1", 2.5m, 1234.5m), Fila(new DateTime(2026, 1, 6), "C2", 3m, 10m) },
                Totales = { ["cantidad"] = 5.5m, ["importe"] = 1244.5m }
            };

            using var libro = Abrir(r);
            var h = libro.Worksheet(1);

            Assert.Equal("Mi Empresa", h.Cell(1, 1).GetString());
            Assert.True(h.Cell(1, 1).Style.Font.Bold);
            Assert.Equal("Ventas por documento", h.Cell(2, 1).GetString());
            Assert.Equal("Fechas: 2026-01-01 a 2026-01-31", h.Cell(3, 1).GetString());
            Assert.Equal("Generado por ana el 2026-11-08 10:30", h.Cell(4, 1).GetString());

            // Con totales hay una primera columna de etiquetas ("Total general"); los datos empiezan en la columna 2.
            Assert.Equal("Fecha", h.Cell(6, 2).GetString());
            Assert.Equal("Importe", h.Cell(6, 5).GetString());
            Assert.True(h.Cell(6, 2).Style.Font.Bold);

            Assert.Equal(new DateTime(2026, 1, 5), h.Cell(7, 2).GetDateTime());
            Assert.Equal("yyyy-mm-dd", h.Cell(7, 2).Style.NumberFormat.Format);
            Assert.Equal("C1", h.Cell(7, 3).GetString());
            Assert.Equal(1234.5, h.Cell(7, 5).GetDouble());
            Assert.Equal("#,##0.00", h.Cell(7, 5).Style.NumberFormat.Format);
            Assert.Equal("#,##0.######", h.Cell(7, 4).Style.NumberFormat.Format);

            Assert.Equal("Total general", h.Cell(9, 1).GetString());
            Assert.True(h.Cell(9, 1).Style.Font.Bold);
            Assert.Equal(1244.5, h.Cell(9, 5).GetDouble());
        }

        [Fact]
        public void SinAgrupacion_TieneAutofiltroYEncabezadoFijo()
        {
            var r = new ResultadoReporte { Columnas = { CTexto, CImporte }, Filas = { ReporteFalso.Fila(("cliente", "A"), ("importe", 1m)) } };

            using var libro = Abrir(r);
            var h = libro.Worksheet(1);

            Assert.True(h.AutoFilter.IsEnabled);
            Assert.Equal(6, h.SheetView.SplitRow);
        }

        [Fact]
        public void ConAgrupacion_EncabezadoDeGrupoSubtotalYTotalEnNegrita()
        {
            var grupo = new GrupoReporte
            {
                Campo = "cliente", Titulo = "Cliente", Etiqueta = "A", Clave = "A", Conteo = 2,
                Subtotales = { ["cantidad"] = 3m, ["importe"] = 30m },
                Filas = { Fila(new DateTime(2026, 1, 5), "A", 1m, 10m), Fila(new DateTime(2026, 1, 6), "A", 2m, 20m) }
            };
            var r = new ResultadoReporte
            {
                Columnas = { CFecha, CCant, CImporte },
                Grupos = { grupo },
                Totales = { ["cantidad"] = 3m, ["importe"] = 30m }
            };

            using var libro = Abrir(r);
            var h = libro.Worksheet(1);

            Assert.False(h.AutoFilter.IsEnabled);
            Assert.Equal("Cliente: A (2)", h.Cell(7, 1).GetString());
            Assert.True(h.Cell(7, 1).Style.Font.Bold);
            Assert.Equal(new DateTime(2026, 1, 5), h.Cell(8, 2).GetDateTime());
            Assert.Equal("Subtotal A", h.Cell(10, 1).GetString());
            Assert.True(h.Cell(10, 1).Style.Font.Bold);
            Assert.Equal(30.0, h.Cell(10, 4).GetDouble());
            Assert.Equal("Total general", h.Cell(11, 1).GetString());
            Assert.Equal(30.0, h.Cell(11, 4).GetDouble());
        }

        [Fact]
        public void ElNombreDeLaHojaSeSaneaYSeRecortaA31()
        {
            var def = Def with { Nombre = "Ventas: [por] documento / año con un nombre muy largo para Excel" };
            var bytes = new ExcelExportador().Exportar(new ResultadoReporte { Columnas = { CTexto } }, def, Ctx);

            using var libro = new XLWorkbook(new MemoryStream(bytes));
            var nombre = libro.Worksheet(1).Name;

            Assert.True(nombre.Length <= 31);
            Assert.DoesNotContain(':', nombre);
            Assert.DoesNotContain('[', nombre);
            Assert.DoesNotContain('/', nombre);
        }

        [Fact]
        public void LosTextosQueEmpiezanConIgualSeGuardanComoTextoYNuncaComoFormula()
        {
            var grupo = new GrupoReporte
            {
                Campo = "cliente", Titulo = "Cliente", Etiqueta = "=1+1", Clave = "=1+1", Conteo = 1,
                Subtotales = { ["importe"] = 1m },
                Filas = { ReporteFalso.Fila(("cliente", "=1+1"), ("importe", 1m)) }
            };
            var r = new ResultadoReporte { Columnas = { CTexto, CImporte }, Grupos = { grupo } };
            var ctx = Ctx with { Empresa = "=1+1", FiltrosTexto = "=SUM(1,2)" };

            using var libro = new XLWorkbook(new MemoryStream(new ExcelExportador().Exportar(r, Def, ctx)));
            var h = libro.Worksheet(1);

            Assert.Equal("=1+1", h.Cell(1, 1).GetString());
            Assert.False(h.Cell(1, 1).HasFormula);
            Assert.Equal("=SUM(1,2)", h.Cell(3, 1).GetString());
            Assert.False(h.Cell(3, 1).HasFormula);
            Assert.Equal("Cliente: =1+1 (1)", h.Cell(7, 1).GetString());
            Assert.Equal("=1+1", h.Cell(8, 2).GetString());
            Assert.Equal(XLDataType.Text, h.Cell(8, 2).DataType);
            Assert.False(h.Cell(8, 2).HasFormula);
            Assert.Equal("Subtotal =1+1", h.Cell(9, 1).GetString());
            Assert.False(h.Cell(9, 1).HasFormula);
        }

        private const string TextoTruncado = "El resultado se truncó al máximo de filas: refine los filtros para ver el resto.";

        [Fact]
        public void EscribeLosAvisosYElTruncadoEnLaFila5SinMoverLaTabla()
        {
            var r = new ResultadoReporte
            {
                Columnas = { CTexto, CImporte },
                Filas = { ReporteFalso.Fila(("cliente", "A"), ("importe", 1m)) },
                Truncado = true,
                Avisos = { "El reporte mezcla monedas: los importes se muestran sin convertir." }
            };

            using var libro = Abrir(r);
            var h = libro.Worksheet(1);

            Assert.Equal("El reporte mezcla monedas: los importes se muestran sin convertir. " + TextoTruncado, h.Cell(5, 1).GetString());
            Assert.True(h.Cell(5, 1).Style.Font.Italic);
            Assert.Equal("Cliente", h.Cell(6, 1).GetString());
            Assert.Equal("A", h.Cell(7, 1).GetString());
        }

        [Fact]
        public void SoloTruncado_EscribeElTextoDeTruncado()
        {
            var r = new ResultadoReporte { Columnas = { CTexto }, Truncado = true };

            using var libro = Abrir(r);

            Assert.Equal(TextoTruncado, libro.Worksheet(1).Cell(5, 1).GetString());
        }

        [Fact]
        public void SinAvisosNiTruncado_LaFila5QuedaVacia()
        {
            var r = new ResultadoReporte { Columnas = { CTexto }, Filas = { ReporteFalso.Fila(("cliente", "A")) } };

            using var libro = Abrir(r);

            Assert.True(libro.Worksheet(1).Cell(5, 1).IsEmpty());
        }

        [Fact]
        public void ElResultadoVacioGeneraUnLibroValido()
        {
            using var libro = Abrir(new ResultadoReporte { Columnas = { CTexto, CImporte } });

            Assert.Equal("Cliente", libro.Worksheet(1).Cell(6, 1).GetString());
        }
    }
}
