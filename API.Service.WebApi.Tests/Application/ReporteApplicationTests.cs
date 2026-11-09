using System.Text.Json;
using API.Application.DTO.reporte;
using API.Application.Main;
using API.Domain.Interface.Reportes;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class ReporteApplicationTests
    {
        private readonly Mock<ICatalogoReportes> _catalogo = new();
        private readonly Mock<IMotorReportes> _motor = new();
        private readonly Mock<IReporte> _ventas = new();
        private readonly Mock<IReporte> _stock = new();
        private readonly Mock<IExportadorReporte> _csv = new();
        private readonly Mock<IReporteVistaDomain> _vistas = new();
        private readonly ReporteApplication _app;

        private static ReporteDefinicion Def(string codigo, string modulo) =>
            new(codigo, "Nombre " + codigo, "Desc " + codigo, "Familia", modulo, new[] { new FiltroReporte("fechas", "Fechas", TipoFiltro.RangoFechas) }, Array.Empty<ColumnaReporte>());

        public ReporteApplicationTests()
        {
            _ventas.SetupGet(r => r.Definicion).Returns(Def("ventas", "ReportesVentas"));
            _stock.SetupGet(r => r.Definicion).Returns(Def("stock", "ReportesInventario"));
            _catalogo.SetupGet(c => c.Todos).Returns(new[] { _ventas.Object, _stock.Object });
            _catalogo.Setup(c => c.Buscar("ventas")).Returns(_ventas.Object);
            _catalogo.Setup(c => c.Buscar("nada")).Returns((IReporte?)null);
            _csv.SetupGet(e => e.Formato).Returns("csv");
            _csv.SetupGet(e => e.ContentType).Returns("text/csv");
            _csv.SetupGet(e => e.Extension).Returns("csv");
            _app = new ReporteApplication(_catalogo.Object, _motor.Object, new[] { _csv.Object }, _vistas.Object);
        }

        [Fact]
        public void ObtenerCatalogo_SoloIncluyeLosReportesDeModulosPermitidos()
        {
            var r = _app.ObtenerCatalogo(modulo => modulo == "ReportesVentas");

            Assert.True(r.Resultado);
            var item = Assert.Single(r.Dato);
            Assert.Equal("ventas", item.Codigo);
            Assert.Equal("Nombre ventas", item.Nombre);
            Assert.Equal("Desc ventas", item.Descripcion);
            Assert.Equal("Familia", item.Familia);
        }

        [Fact]
        public void ObtenerDefinicion_Existente_LaDevuelve()
        {
            var r = _app.ObtenerDefinicion("ventas");

            Assert.True(r.Resultado);
            Assert.Equal("ventas", r.Dato.Codigo);
        }

        [Fact]
        public void ObtenerDefinicion_Inexistente_DiceQueNoExiste()
        {
            var r = _app.ObtenerDefinicion("nada");

            Assert.False(r.Resultado);
            Assert.Equal("El reporte 'nada' no existe.", r.Mensaje);
        }

        [Fact]
        public async Task Ejecutar_PasaLaSolicitudAlMotorYDevuelveElResultado()
        {
            var solicitud = new SolicitudReporte();
            var resultado = new ResultadoReporte { TotalFilas = 3 };
            _motor.Setup(m => m.EjecutarAsync(_ventas.Object, solicitud)).ReturnsAsync(resultado);

            var r = await _app.EjecutarAsync("ventas", solicitud);

            Assert.True(r.Resultado);
            Assert.Same(resultado, r.Dato);
        }

        [Fact]
        public async Task Ejecutar_ReporteInexistente_DiceQueNoExiste()
        {
            var r = await _app.EjecutarAsync("nada", new SolicitudReporte());

            Assert.False(r.Resultado);
            Assert.Equal("El reporte 'nada' no existe.", r.Mensaje);
            _motor.Verify(m => m.EjecutarAsync(It.IsAny<IReporte>(), It.IsAny<SolicitudReporte>()), Times.Never);
        }

        [Fact]
        public async Task Ejecutar_SolicitudInvalida_DevuelveElMensajeDelMotor()
        {
            _motor.Setup(m => m.EjecutarAsync(It.IsAny<IReporte>(), It.IsAny<SolicitudReporte>()))
                  .ThrowsAsync(new ReporteInvalidoException("El filtro 'Cliente' es obligatorio."));

            var r = await _app.EjecutarAsync("ventas", new SolicitudReporte());

            Assert.False(r.Resultado);
            Assert.Equal("El filtro 'Cliente' es obligatorio.", r.Mensaje);
        }

        // ---------- exportar ----------

        private static ExportarReporteRequest Pedido(string? grafico = null, Action<SolicitudReporte>? ajustar = null)
        {
            var req = new ExportarReporteRequest { GraficoPng = grafico };
            ajustar?.Invoke(req.Solicitud);
            return req;
        }

        // PNG mínimo válido de 1x1.
        private const string PngDe1x1 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

        [Fact]
        public async Task Exportar_GeneraElArchivoConElExportadorDelFormatoYSuContexto()
        {
            var resultado = new ResultadoReporte();
            var solicitud = new SolicitudReporte();
            solicitud.Filtros["fechas"] = new FiltroValor { Desde = "2026-01-01", Hasta = "2026-01-31" };
            _motor.Setup(m => m.EjecutarAsync(_ventas.Object, solicitud)).ReturnsAsync(resultado);
            ContextoExportacion? contexto = null;
            _csv.Setup(e => e.Exportar(resultado, It.IsAny<ReporteDefinicion>(), It.IsAny<ContextoExportacion>()))
                .Callback<ResultadoReporte, ReporteDefinicion, ContextoExportacion>((_, _, c) => contexto = c)
                .Returns(new byte[] { 1, 2, 3 });

            var r = await _app.ExportarAsync("ventas", new ExportarReporteRequest { Solicitud = solicitud, GraficoPng = PngDe1x1 }, "CSV", "Mi Empresa", "ana");

            Assert.True(r.Resultado);
            Assert.Equal(new byte[] { 1, 2, 3 }, r.Dato.Contenido);
            Assert.Equal("text/csv", r.Dato.ContentType);
            Assert.Matches(@"^ventas-\d{8}-\d{4}\.csv$", r.Dato.NombreArchivo);
            Assert.Equal("Mi Empresa", contexto!.Empresa);
            Assert.Equal("ana", contexto.Usuario);
            Assert.Equal("Fechas: 2026-01-01 a 2026-01-31", contexto.FiltrosTexto);
            Assert.NotNull(contexto.GraficoPng);
        }

        [Fact]
        public async Task Exportar_SinFiltros_DiceSinFiltros()
        {
            ContextoExportacion? contexto = null;
            _motor.Setup(m => m.EjecutarAsync(_ventas.Object, It.IsAny<SolicitudReporte>())).ReturnsAsync(new ResultadoReporte());
            _csv.Setup(e => e.Exportar(It.IsAny<ResultadoReporte>(), It.IsAny<ReporteDefinicion>(), It.IsAny<ContextoExportacion>()))
                .Callback<ResultadoReporte, ReporteDefinicion, ContextoExportacion>((_, _, c) => contexto = c)
                .Returns(new byte[] { 1 });

            await _app.ExportarAsync("ventas", Pedido(), "csv", "E", "u");

            Assert.Equal("Sin filtros", contexto!.FiltrosTexto);
            Assert.Null(contexto.GraficoPng);
        }

        [Fact]
        public async Task Exportar_FormatoInvalido_NoEjecutaNada()
        {
            var r = await _app.ExportarAsync("ventas", Pedido(), "docx", "E", "u");

            Assert.False(r.Resultado);
            Assert.Equal("El formato 'docx' no es válido. Use xlsx, pdf o csv.", r.Mensaje);
            _motor.Verify(m => m.EjecutarAsync(It.IsAny<IReporte>(), It.IsAny<SolicitudReporte>()), Times.Never);
        }

        [Fact]
        public async Task Exportar_ReporteInexistente_DiceQueNoExiste()
        {
            var r = await _app.ExportarAsync("nada", Pedido(), "csv", "E", "u");

            Assert.False(r.Resultado);
            Assert.Equal("El reporte 'nada' no existe.", r.Mensaje);
        }

        [Fact]
        public async Task Exportar_SolicitudNula_Lanza400DeNegocio()
        {
            var r = await _app.ExportarAsync("ventas", null!, "csv", "E", "u");

            Assert.False(r.Resultado);
            Assert.Equal("La solicitud del reporte es obligatoria.", r.Mensaje);
        }

        [Theory]
        [InlineData("no-es-base64!!", "El gráfico no es una imagen PNG válida.")]
        [InlineData("aGVsbG8gbXVuZG8=", "El gráfico no es una imagen PNG válida.")]
        public async Task Exportar_GraficoInvalido_Rechaza(string grafico, string mensaje)
        {
            var r = await _app.ExportarAsync("ventas", Pedido(grafico), "csv", "E", "u");

            Assert.False(r.Resultado);
            Assert.Equal(mensaje, r.Mensaje);
        }

        [Fact]
        public async Task Exportar_GraficoMayorA2Mb_Rechaza()
        {
            var grande = Convert.ToBase64String(new byte[2 * 1024 * 1024 + 10]);

            var r = await _app.ExportarAsync("ventas", Pedido(grande), "csv", "E", "u");

            Assert.False(r.Resultado);
            Assert.Equal("El gráfico es demasiado grande (máximo 2 MB).", r.Mensaje);
        }

        [Fact]
        public async Task Exportar_AceptaElPrefijoDataUri()
        {
            _motor.Setup(m => m.EjecutarAsync(_ventas.Object, It.IsAny<SolicitudReporte>())).ReturnsAsync(new ResultadoReporte());
            _csv.Setup(e => e.Exportar(It.IsAny<ResultadoReporte>(), It.IsAny<ReporteDefinicion>(), It.IsAny<ContextoExportacion>())).Returns(new byte[] { 1 });

            var r = await _app.ExportarAsync("ventas", Pedido("data:image/png;base64," + PngDe1x1), "csv", "E", "u");

            Assert.True(r.Resultado);
        }

        // ---------- vistas guardadas ----------

        private static JsonElement Json(string texto) => JsonDocument.Parse(texto).RootElement.Clone();

        [Fact]
        public async Task ObtenerVistas_DevuelveLaConfiguracionComoObjetoJson()
        {
            _vistas.Setup(v => v.ObtenerAsync(5, "ventas")).ReturnsAsync(new[]
            {
                new ReporteVistaDato(1, "Mi vista", "{\"columnas\":[\"a\",\"b\"]}", new DateTime(2026, 11, 8))
            });

            var r = await _app.ObtenerVistasAsync(5, "ventas");

            Assert.True(r.Resultado);
            var vista = Assert.Single(r.Dato);
            Assert.Equal("Mi vista", vista.Nombre);
            Assert.Equal(2, vista.Configuracion.GetProperty("columnas").GetArrayLength());
        }

        [Fact]
        public async Task ObtenerVistas_ReporteInexistente_DiceQueNoExiste()
        {
            var r = await _app.ObtenerVistasAsync(5, "nada");

            Assert.False(r.Resultado);
            Assert.Equal("El reporte 'nada' no existe.", r.Mensaje);
        }

        [Fact]
        public async Task GuardarVista_PasaElJsonCrudoAlDominio()
        {
            _vistas.Setup(v => v.GuardarAsync(5, "ventas", "Mi vista", "{\"a\":1}"))
                   .ReturnsAsync(new ReporteVistaDato(7, "Mi vista", "{\"a\":1}", new DateTime(2026, 11, 8)));

            var r = await _app.GuardarVistaAsync(5, "ventas", new GuardarVistaDTO { Nombre = "Mi vista", Configuracion = Json("{\"a\":1}") });

            Assert.True(r.Resultado);
            Assert.Equal(7, r.Dato.Entry);
        }

        [Fact]
        public async Task GuardarVista_SinConfiguracion_Rechaza()
        {
            var r = await _app.GuardarVistaAsync(5, "ventas", new GuardarVistaDTO { Nombre = "V" });

            Assert.False(r.Resultado);
            Assert.Equal("La configuración de la vista no es válida.", r.Mensaje);
        }

        [Fact]
        public async Task GuardarVista_ErrorDelDominio_DevuelveElMensaje()
        {
            _vistas.Setup(v => v.GuardarAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                   .ThrowsAsync(new ReporteInvalidoException("El nombre de la vista es obligatorio."));

            var r = await _app.GuardarVistaAsync(5, "ventas", new GuardarVistaDTO { Nombre = "", Configuracion = Json("{}") });

            Assert.False(r.Resultado);
            Assert.Equal("El nombre de la vista es obligatorio.", r.Mensaje);
        }

        [Fact]
        public async Task EliminarVista_Existente_DevuelveTrue()
        {
            _vistas.Setup(v => v.EliminarAsync(5, "ventas", 7)).ReturnsAsync(true);

            var r = await _app.EliminarVistaAsync(5, "ventas", 7);

            Assert.True(r.Resultado);
            Assert.True(r.Dato);
        }

        [Fact]
        public async Task EliminarVista_Inexistente_DiceQueNoExiste()
        {
            _vistas.Setup(v => v.EliminarAsync(5, "ventas", 7)).ReturnsAsync(false);

            var r = await _app.EliminarVistaAsync(5, "ventas", 7);

            Assert.False(r.Resultado);
            Assert.Equal("La vista no existe.", r.Mensaje);
        }
    }
}
