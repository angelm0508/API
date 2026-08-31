using API.Application.DTO;
using API.Application.DTO.inventario;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class MovimientoInventarioControllerTests
    {
        private readonly Mock<IMovimientoInventarioApplication> _app = new();
        private readonly MovimientoInventarioController _controller;

        public MovimientoInventarioControllerTests() => _controller = new MovimientoInventarioController(_app.Object);

        [Fact]
        public async Task ObtenerPorArticulo_DevuelveOk_YReenviaFiltros()
        {
            var desde = new DateTime(2026, 1, 1);
            var resp = new Respuesta<IEnumerable<MovimientoInventarioDTO>> { Resultado = true, Dato = new List<MovimientoInventarioDTO>() };
            _app.Setup(a => a.ObtenerPorArticuloAsync("ART1", "01", desde, null)).ReturnsAsync(resp);

            var r = await _controller.ObtenerPorArticulo("ART1", "01", desde, null);

            var ok = Assert.IsType<OkObjectResult>(r.Result);
            Assert.Same(resp, ok.Value);
            _app.Verify(a => a.ObtenerPorArticuloAsync("ART1", "01", desde, null), Times.Once);
        }

        [Fact]
        public async Task ObtenerPorArticulo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _app.Setup(a => a.ObtenerPorArticuloAsync("ART1", null, null, null))
                .ReturnsAsync(new Respuesta<IEnumerable<MovimientoInventarioDTO>> { Resultado = false });
            var r = await _controller.ObtenerPorArticulo("ART1", null, null, null);
            Assert.IsType<BadRequestObjectResult>(r.Result);
        }
    }
}
