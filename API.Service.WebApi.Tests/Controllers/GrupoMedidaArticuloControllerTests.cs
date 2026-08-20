using API.Application.DTO;
using API.Application.DTO.articulo.grupo_medida_articulo;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class GrupoMedidaArticuloControllerTests
    {
        private readonly Mock<IGrupoMedidaArticuloApplication> _applicationMock;
        private readonly GrupoMedidaArticuloController _controller;

        public GrupoMedidaArticuloControllerTests()
        {
            _applicationMock = new Mock<IGrupoMedidaArticuloApplication>();
            _controller = new GrupoMedidaArticuloController(_applicationMock.Object);
        }

        [Fact]
        public async Task Obtener_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<GrupoMedidaArticuloDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task Obtener_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<GrupoMedidaArticuloDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<GrupoMedidaArticuloDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("El código del grupo de medida no se encontró.", valor.Mensaje);
        }

        [Fact]
        public async Task Obtener_DevuelveOk_CuandoExiste()
        {
            var dto = new GrupoMedidaArticuloDTO { Codigo = 1, Nombre = "Grupo Medida 1" };
            var respuesta = new Respuesta<GrupoMedidaArticuloDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<GrupoMedidaArticuloDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerAsync("Grupo Medida 1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorNombre("Grupo Medida 1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<GrupoMedidaArticuloDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync("Grupo Medida 1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorNombre("Grupo Medida 1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<GrupoMedidaArticuloDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("El nombre del grupo de medida no se encontró.", valor.Mensaje);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveOk_CuandoExiste()
        {
            var dto = new GrupoMedidaArticuloDTO { Codigo = 1, Nombre = "Grupo Medida 1" };
            var respuesta = new Respuesta<GrupoMedidaArticuloDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerAsync("Grupo Medida 1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorNombre("Grupo Medida 1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObteneContengaNombreAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<GrupoMedidaArticuloDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Gru")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObteneContengaNombreAsync("Gru");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObteneContengaNombreAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<GrupoMedidaArticuloDTO>> { Resultado = true, Dato = new List<GrupoMedidaArticuloDTO> { new GrupoMedidaArticuloDTO { Codigo = 1 } } };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Gru")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObteneContengaNombreAsync("Gru");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<GrupoMedidaArticuloDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerTodoAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodoAsync();

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<GrupoMedidaArticuloDTO>> { Resultado = true, Dato = new List<GrupoMedidaArticuloDTO> { new GrupoMedidaArticuloDTO { Codigo = 1 } } };
            _applicationMock.Setup(a => a.ObtenerTodoAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodoAsync();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new GrupoMedidaArticuloCrearDTO { Nombre = "Grupo Medida 1" };
            var respuesta = new Respuesta<int> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.InsertarAsync(crearDto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new GrupoMedidaArticuloCrearDTO { Nombre = "Grupo Medida 1" };
            var respuesta = new Respuesta<int> { Resultado = true, Dato = 1 };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.InsertarAsync(crearDto);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<GrupoMedidaArticuloDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.ActualizarAsync(1, new GrupoMedidaArticuloActualizarDTO());

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<GrupoMedidaArticuloDTO> { Resultado = true, Dato = new GrupoMedidaArticuloDTO { Codigo = 1 } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ActualizarAsync(1, It.IsAny<GrupoMedidaArticuloActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.ActualizarAsync(1, new GrupoMedidaArticuloActualizarDTO());

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, badRequest.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<GrupoMedidaArticuloDTO> { Resultado = true, Dato = new GrupoMedidaArticuloDTO { Codigo = 1 } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.ActualizarAsync(1, It.IsAny<GrupoMedidaArticuloActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.ActualizarAsync(1, new GrupoMedidaArticuloActualizarDTO());

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, ok.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<GrupoMedidaArticuloDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.EliminarAsync(1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<GrupoMedidaArticuloDTO> { Resultado = true, Dato = new GrupoMedidaArticuloDTO { Codigo = 1 } });
            var respuestaDelete = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.EliminarAsync(1)).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.EliminarAsync(1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, badRequest.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<GrupoMedidaArticuloDTO> { Resultado = true, Dato = new GrupoMedidaArticuloDTO { Codigo = 1 } });
            var respuestaDelete = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.EliminarAsync(1)).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.EliminarAsync(1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, ok.Value);
        }
    }
}
