using API.Application.Main;
using API.Domain.Interface.Pos;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class PosApplicationTests
    {
        private readonly Mock<IPosDomain> _domain = new();
        private readonly PosApplication _app;

        public PosApplicationTests()
        {
            _app = new PosApplication(_domain.Object);
        }

        private static readonly PosConfiguracionDato Dato = new(1, 2, "ALM1", "IVA", "C1", "EFE", "GTQ");

        [Fact]
        public async Task ObtenerConfiguracion_Exito_DevuelveLaVista()
        {
            var vista = new PosConfiguracionVista(Dato, Array.Empty<string>());
            _domain.Setup(d => d.ObtenerConfiguracionAsync()).ReturnsAsync(vista);

            var r = await _app.ObtenerConfiguracionAsync();

            Assert.True(r.Resultado);
            Assert.Same(vista, r.Dato);
            Assert.False(string.IsNullOrEmpty(r.Mensaje));
        }

        [Fact]
        public async Task ObtenerConfiguracion_Excepcion_DevuelveResultadoFalsoConElMensaje()
        {
            _domain.Setup(d => d.ObtenerConfiguracionAsync()).ThrowsAsync(new InvalidOperationException("boom"));

            var r = await _app.ObtenerConfiguracionAsync();

            Assert.False(r.Resultado);
            Assert.Equal("boom", r.Mensaje);
        }

        [Fact]
        public async Task GuardarConfiguracion_Exito_GuardaYDevuelveLaVistaActualizada()
        {
            var vista = new PosConfiguracionVista(Dato, Array.Empty<string>());
            _domain.Setup(d => d.GuardarConfiguracionAsync(Dato)).Returns(Task.CompletedTask);
            _domain.Setup(d => d.ObtenerConfiguracionAsync()).ReturnsAsync(vista);

            var r = await _app.GuardarConfiguracionAsync(Dato);

            Assert.True(r.Resultado);
            Assert.Same(vista, r.Dato);
            _domain.Verify(d => d.GuardarConfiguracionAsync(Dato), Times.Once);
        }

        [Fact]
        public async Task GuardarConfiguracion_Excepcion_DevuelveResultadoFalsoConElMensaje()
        {
            _domain.Setup(d => d.GuardarConfiguracionAsync(Dato)).ThrowsAsync(new InvalidOperationException("El almacén no existe."));

            var r = await _app.GuardarConfiguracionAsync(Dato);

            Assert.False(r.Resultado);
            Assert.Equal("El almacén no existe.", r.Mensaje);
            _domain.Verify(d => d.ObtenerConfiguracionAsync(), Times.Never);
        }

        [Fact]
        public async Task LosDemasMetodos_DelegaEnElDominioYAtrapanExcepciones()
        {
            _domain.Setup(d => d.ObtenerRapidosAsync()).ThrowsAsync(new NotImplementedException("pendiente"));
            _domain.Setup(d => d.ObtenerTicketAsync(7)).ReturnsAsync((TicketPos)null!);
            _domain.Setup(d => d.ObtenerVentasDelDiaAsync()).ReturnsAsync(Array.Empty<VentaPosDelDia>());

            var rapidos = await _app.ObtenerRapidosAsync();
            var ticket = await _app.ObtenerTicketAsync(7);
            var dia = await _app.ObtenerVentasDelDiaAsync();

            Assert.False(rapidos.Resultado);
            Assert.Equal("pendiente", rapidos.Mensaje);
            Assert.True(ticket.Resultado);
            Assert.True(dia.Resultado);
        }

        [Fact]
        public async Task Catalogo_Rapidos_Exito_DevuelveLosArticulos()
        {
            var articulos = new List<ArticuloPos> { new("A1", null, "Art", 5m, 3m, true) };
            _domain.Setup(d => d.ObtenerRapidosAsync()).ReturnsAsync(articulos);

            var r = await _app.ObtenerRapidosAsync();

            Assert.True(r.Resultado);
            Assert.Same(articulos, r.Dato);
        }

        [Fact]
        public async Task Catalogo_Buscar_ConExcepcion_DevuelveResultadoFalsoConElMensaje()
        {
            _domain.Setup(d => d.BuscarArticulosAsync("ab", "C1")).ThrowsAsync(new InvalidOperationException("sin conexión"));

            var r = await _app.BuscarArticulosAsync("ab", "C1");

            Assert.False(r.Resultado);
            Assert.Equal("sin conexión", r.Mensaje);
        }

        [Fact]
        public async Task Catalogo_PorCodigo_SinArticulo_EsConsultaCorrectaConDatoNulo()
        {
            _domain.Setup(d => d.ObtenerArticuloPorCodigoAsync("X", null)).ReturnsAsync((ArticuloPos?)null);

            var r = await _app.ObtenerArticuloPorCodigoAsync("X", null);

            Assert.True(r.Resultado);
            Assert.Null(r.Dato);
        }

        [Fact]
        public async Task Catalogo_PorCodigo_ConExcepcion_DevuelveResultadoFalsoConElMensaje()
        {
            _domain.Setup(d => d.ObtenerArticuloPorCodigoAsync("X", null)).ThrowsAsync(new Exception("falló"));

            var r = await _app.ObtenerArticuloPorCodigoAsync("X", null);

            Assert.False(r.Resultado);
            Assert.Equal("falló", r.Mensaje);
        }

        [Fact]
        public async Task BuscarObtenerPorCodigoYVender_DelegaEnElDominio()
        {
            var articulos = new List<ArticuloPos> { new("A1", null, "Art", 5m, 3m, true) };
            _domain.Setup(d => d.BuscarArticulosAsync("a", "C1")).ReturnsAsync(articulos);
            _domain.Setup(d => d.ObtenerArticuloPorCodigoAsync("A1", "C1")).ReturnsAsync(articulos[0]);
            var solicitud = new VentaPosSolicitud(null, null, new(), new());
            var ticket = new TicketPos(1, 1, "S", 1, DateTime.Today, "caja", "C1", "N", "CF", Array.Empty<TicketPosLinea>(), 0, 0, 0, Array.Empty<TicketPosPago>(), 0, 0, "GTQ", "");
            _domain.Setup(d => d.VenderAsync(solicitud, "caja")).ReturnsAsync(ticket);

            var b = await _app.BuscarArticulosAsync("a", "C1");
            var c = await _app.ObtenerArticuloPorCodigoAsync("A1", "C1");
            var v = await _app.VenderAsync(solicitud, "caja");

            Assert.Same(articulos, b.Dato);
            Assert.Same(articulos[0], c.Dato);
            Assert.Same(ticket, v.Dato);
            Assert.True(b.Resultado && c.Resultado && v.Resultado);
        }
    }
}
