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
        public async Task ObtenerContexto_Exito_DevuelveElContexto_YExcepcionDevuelveElMensaje()
        {
            var contexto = new PosContexto(new PosConfiguracionVista(Dato, Array.Empty<string>()),
                new[] { new FormaPagoPos("EFE", "Efectivo", "N") }, 12m, "Consumidor", "CF");
            _domain.Setup(d => d.ObtenerContextoAsync()).ReturnsAsync(contexto);

            var r = await _app.ObtenerContextoAsync();
            Assert.True(r.Resultado);
            Assert.Same(contexto, r.Dato);

            _domain.Setup(d => d.ObtenerContextoAsync()).ThrowsAsync(new InvalidOperationException("boom"));
            var e = await _app.ObtenerContextoAsync();
            Assert.False(e.Resultado);
            Assert.Equal("boom", e.Mensaje);
        }

        [Fact]
        public async Task BuscarClientes_PasaElTextoYDevuelveLaLista()
        {
            IReadOnlyList<ClientePos> lista = new[] { new ClientePos("C1", "Juan", "123", 3) };
            _domain.Setup(d => d.BuscarClientesAsync("ju")).ReturnsAsync(lista);

            var r = await _app.BuscarClientesAsync("ju");

            Assert.True(r.Resultado);
            Assert.Same(lista, r.Dato);
        }

        [Fact]
        public async Task CrearCliente_Exito_DevuelveElCliente_YExcepcionDevuelveElMensaje()
        {
            var solicitud = new NuevoClientePos("Juan", "1234567-8");
            var cliente = new ClientePos("CLI-1", "Juan", "1234567-8", null);
            _domain.Setup(d => d.CrearClienteAsync(solicitud)).ReturnsAsync(cliente);

            var r = await _app.CrearClienteAsync(solicitud);
            Assert.True(r.Resultado);
            Assert.Same(cliente, r.Dato);
            Assert.Equal("Cliente creado correctamente.", r.Mensaje);

            _domain.Setup(d => d.CrearClienteAsync(solicitud)).ThrowsAsync(new Exception("El nombre del cliente es obligatorio."));
            var e = await _app.CrearClienteAsync(solicitud);
            Assert.False(e.Resultado);
            Assert.Null(e.Dato);
            Assert.Equal("El nombre del cliente es obligatorio.", e.Mensaje);
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
        public async Task Vender_ConExcepcionDeNegocio_DevuelveResultadoFalsoConElMensaje()
        {
            var solicitud = new VentaPosSolicitud(null, null, new(), new());
            _domain.Setup(d => d.VenderAsync(solicitud, "ana")).ThrowsAsync(new Exception("Agrega al menos un artículo a la venta."));

            var r = await _app.VenderAsync(solicitud, "ana");

            Assert.False(r.Resultado);
            Assert.Null(r.Dato);
            Assert.Equal("Agrega al menos un artículo a la venta.", r.Mensaje);
        }

        [Fact]
        public async Task Vender_Exito_MensajeDeVentaRegistrada()
        {
            var solicitud = new VentaPosSolicitud(null, null, new(), new());
            var ticket = new TicketPos(1, 1, "S", 1, DateTime.Today, "ana", "C1", "N", "CF", Array.Empty<TicketPosLinea>(), 0, 0, 0, Array.Empty<TicketPosPago>(), 0, 0, "GTQ", "");
            _domain.Setup(d => d.VenderAsync(solicitud, "ana")).ReturnsAsync(ticket);

            var r = await _app.VenderAsync(solicitud, "ana");

            Assert.True(r.Resultado);
            Assert.Same(ticket, r.Dato);
            Assert.Equal("Venta registrada correctamente.", r.Mensaje);
        }

        [Fact]
        public async Task Ticket_LaVentaNoExiste_DevuelveResultadoFalsoConElMensaje()
        {
            _domain.Setup(d => d.ObtenerTicketAsync(9)).ThrowsAsync(new Exception("La venta no existe."));

            var r = await _app.ObtenerTicketAsync(9);

            Assert.False(r.Resultado);
            Assert.Equal("La venta no existe.", r.Mensaje);
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
