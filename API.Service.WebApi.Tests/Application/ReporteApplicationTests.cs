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
        private readonly ReporteApplication _app;

        private static ReporteDefinicion Def(string codigo, string modulo) =>
            new(codigo, "Nombre " + codigo, "Desc " + codigo, "Familia", modulo, Array.Empty<FiltroReporte>(), Array.Empty<ColumnaReporte>());

        public ReporteApplicationTests()
        {
            _ventas.SetupGet(r => r.Definicion).Returns(Def("ventas", "ReportesVentas"));
            _stock.SetupGet(r => r.Definicion).Returns(Def("stock", "ReportesInventario"));
            _catalogo.SetupGet(c => c.Todos).Returns(new[] { _ventas.Object, _stock.Object });
            _catalogo.Setup(c => c.Buscar("ventas")).Returns(_ventas.Object);
            _catalogo.Setup(c => c.Buscar("nada")).Returns((IReporte?)null);
            _app = new ReporteApplication(_catalogo.Object, _motor.Object);
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
    }
}
