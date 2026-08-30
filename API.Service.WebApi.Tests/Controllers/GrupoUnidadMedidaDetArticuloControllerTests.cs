using API.Application.DTO;
using API.Application.DTO.articulo.grupo_unidad_medida_det_articulo;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class GrupoUnidadMedidaDetArticuloControllerTests
    {
        private readonly Mock<IGrupoUnidadMedidaDetArticuloApplication> _applicationMock;
        private readonly GrupoUnidadMedidaDetArticuloController _controller;

        public GrupoUnidadMedidaDetArticuloControllerTests()
        {
            _applicationMock = new Mock<IGrupoUnidadMedidaDetArticuloApplication>();
            _controller = new GrupoUnidadMedidaDetArticuloController(_applicationMock.Object);
        }

        [Fact]
        public async Task Obtener_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<GrupoUnidadMedidaDetArticuloDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1, 1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task Obtener_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<GrupoUnidadMedidaDetArticuloDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1, 1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<GrupoUnidadMedidaDetArticuloDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
        }

        [Fact]
        public async Task Obtener_DevuelveOk_CuandoExiste()
        {
            var dto = new GrupoUnidadMedidaDetArticuloDTO { GrpMedidaEntry = 1, NumLinea = 1, MedidaEntry = 5 };
            var respuesta = new Respuesta<GrupoUnidadMedidaDetArticuloDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1, 1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorGrupo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<GrupoUnidadMedidaDetArticuloDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerPorGrupoAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorGrupo(1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerPorGrupo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<GrupoUnidadMedidaDetArticuloDTO>>
            {
                Resultado = true,
                Dato = new List<GrupoUnidadMedidaDetArticuloDTO> { new GrupoUnidadMedidaDetArticuloDTO { GrpMedidaEntry = 1, NumLinea = 1 } }
            };
            _applicationMock.Setup(a => a.ObtenerPorGrupoAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorGrupo(1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<GrupoUnidadMedidaDetArticuloDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerTodoAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodoAsync();

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<GrupoUnidadMedidaDetArticuloDTO>>
            {
                Resultado = true,
                Dato = new List<GrupoUnidadMedidaDetArticuloDTO> { new GrupoUnidadMedidaDetArticuloDTO { GrpMedidaEntry = 1, NumLinea = 1 } }
            };
            _applicationMock.Setup(a => a.ObtenerTodoAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodoAsync();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new GrupoUnidadMedidaDetArticuloCrearDTO { GrpMedidaEntry = 1, MedidaEntry = 5 };
            var respuesta = new Respuesta<int> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.InsertarAsync(crearDto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new GrupoUnidadMedidaDetArticuloCrearDTO { GrpMedidaEntry = 1, MedidaEntry = 5 };
            var respuesta = new Respuesta<int> { Resultado = true, Dato = 1 };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.InsertarAsync(crearDto);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<GrupoUnidadMedidaDetArticuloDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.ActualizarAsync(1, 1, new GrupoUnidadMedidaDetArticuloActualizarDTO());

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1))
                .ReturnsAsync(new Respuesta<GrupoUnidadMedidaDetArticuloDTO> { Resultado = true, Dato = new GrupoUnidadMedidaDetArticuloDTO { GrpMedidaEntry = 1, NumLinea = 1 } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ActualizarAsync(1, 1, It.IsAny<GrupoUnidadMedidaDetArticuloActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.ActualizarAsync(1, 1, new GrupoUnidadMedidaDetArticuloActualizarDTO());

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, badRequest.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1))
                .ReturnsAsync(new Respuesta<GrupoUnidadMedidaDetArticuloDTO> { Resultado = true, Dato = new GrupoUnidadMedidaDetArticuloDTO { GrpMedidaEntry = 1, NumLinea = 1 } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.ActualizarAsync(1, 1, It.IsAny<GrupoUnidadMedidaDetArticuloActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.ActualizarAsync(1, 1, new GrupoUnidadMedidaDetArticuloActualizarDTO());

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, ok.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<GrupoUnidadMedidaDetArticuloDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.EliminarAsync(1, 1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1))
                .ReturnsAsync(new Respuesta<GrupoUnidadMedidaDetArticuloDTO> { Resultado = true, Dato = new GrupoUnidadMedidaDetArticuloDTO { GrpMedidaEntry = 1, NumLinea = 1 } });
            var respuestaDelete = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.EliminarAsync(1, 1)).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.EliminarAsync(1, 1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, badRequest.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1))
                .ReturnsAsync(new Respuesta<GrupoUnidadMedidaDetArticuloDTO> { Resultado = true, Dato = new GrupoUnidadMedidaDetArticuloDTO { GrpMedidaEntry = 1, NumLinea = 1 } });
            var respuestaDelete = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.EliminarAsync(1, 1)).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.EliminarAsync(1, 1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, ok.Value);
        }
    }
}
