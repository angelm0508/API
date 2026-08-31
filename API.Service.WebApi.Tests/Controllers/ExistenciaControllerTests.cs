using API.Application.DTO;
using API.Application.DTO.inventario;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class ExistenciaControllerTests
    {
        private readonly Mock<IExistenciaApplication> _app = new();
        private readonly ExistenciaController _controller;

        public ExistenciaControllerTests() => _controller = new ExistenciaController(_app.Object);

        [Fact]
        public async Task ObtenerTodo_DevuelveOk_ConFiltros()
        {
            var resp = new Respuesta<IEnumerable<ExistenciaArticuloDTO>> { Resultado = true, Dato = new List<ExistenciaArticuloDTO>() };
            _app.Setup(a => a.ObtenerTodoAsync("ART1", "01")).ReturnsAsync(resp);

            var r = await _controller.ObtenerTodo("ART1", "01");

            var ok = Assert.IsType<OkObjectResult>(r.Result);
            Assert.Same(resp, ok.Value);
            _app.Verify(a => a.ObtenerTodoAsync("ART1", "01"), Times.Once);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _app.Setup(a => a.ObtenerTodoAsync(null, null)).ReturnsAsync(new Respuesta<IEnumerable<ExistenciaArticuloDTO>> { Resultado = false });
            var r = await _controller.ObtenerTodo(null, null);
            Assert.IsType<BadRequestObjectResult>(r.Result);
        }

        [Fact]
        public async Task Obtener_DevuelveOk()
        {
            var resp = new Respuesta<ExistenciaArticuloDTO> { Resultado = true, Dato = new ExistenciaArticuloDTO { CodArticulo = "ART1", CodAlmacen = "01" } };
            _app.Setup(a => a.ObtenerAsync("ART1", "01")).ReturnsAsync(resp);
            var r = await _controller.Obtener("ART1", "01");
            var ok = Assert.IsType<OkObjectResult>(r.Result);
            Assert.Same(resp, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorArticulo_DevuelveOk()
        {
            var resp = new Respuesta<IEnumerable<ExistenciaArticuloDTO>> { Resultado = true, Dato = new List<ExistenciaArticuloDTO>() };
            _app.Setup(a => a.ObtenerPorArticuloAsync("ART1")).ReturnsAsync(resp);
            var r = await _controller.ObtenerPorArticulo("ART1");
            Assert.IsType<OkObjectResult>(r.Result);
        }
    }
}
