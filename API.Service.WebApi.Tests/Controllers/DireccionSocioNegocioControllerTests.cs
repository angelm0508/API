using API.Application.DTO;
using API.Application.DTO.direccionSocioNegocio;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class DireccionSocioNegocioControllerTests
    {
        private readonly Mock<IDireccionSocioNegocioApplication> _applicationMock;
        private readonly DireccionSocioNegocioController _controller;

        public DireccionSocioNegocioControllerTests()
        {
            _applicationMock = new Mock<IDireccionSocioNegocioApplication>();
            _controller = new DireccionSocioNegocioController(_applicationMock.Object);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<DireccionSocioNegocioDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("D1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<DireccionSocioNegocioDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("D1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<DireccionSocioNegocioDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("Código de dirección no encontrado.", valor.Mensaje);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveOk_CuandoExiste()
        {
            var dto = new DireccionSocioNegocioDTO { Direccion = "D1", CodigoSn = "SN1" };
            var respuesta = new Respuesta<DireccionSocioNegocioDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("D1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<DireccionSocioNegocioDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("D")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaCodigo("D");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<DireccionSocioNegocioDTO>> { Resultado = true, Dato = new List<DireccionSocioNegocioDTO> { new DireccionSocioNegocioDTO { Direccion = "D1", CodigoSn = "SN1" } } };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("D")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaCodigo("D");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<DireccionSocioNegocioDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodo();

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<DireccionSocioNegocioDTO>> { Resultado = true, Dato = new List<DireccionSocioNegocioDTO> { new DireccionSocioNegocioDTO { Direccion = "D1", CodigoSn = "SN1" } } };
            _applicationMock.Setup(a => a.ObtenerAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodo();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task Crear_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new DireccionSocioNegocioCrearDTO { Direccion = "D1", CodigoSn = "SN1" };
            var respuesta = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.Crear(crearDto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task Crear_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new DireccionSocioNegocioCrearDTO { Direccion = "D1", CodigoSn = "SN1" };
            var respuesta = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.Crear(crearDto);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<DireccionSocioNegocioDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1")).ReturnsAsync(respuesta);

            var resultado = await _controller.Actualizar("D1", new DireccionSocioNegocioActualizarDTO());

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1"))
                .ReturnsAsync(new Respuesta<DireccionSocioNegocioDTO> { Resultado = true, Dato = new DireccionSocioNegocioDTO { Direccion = "D1", CodigoSn = "SN1" } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ActualizarAsync("D1", It.IsAny<DireccionSocioNegocioActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.Actualizar("D1", new DireccionSocioNegocioActualizarDTO());

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, badRequest.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1"))
                .ReturnsAsync(new Respuesta<DireccionSocioNegocioDTO> { Resultado = true, Dato = new DireccionSocioNegocioDTO { Direccion = "D1", CodigoSn = "SN1" } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.ActualizarAsync("D1", It.IsAny<DireccionSocioNegocioActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.Actualizar("D1", new DireccionSocioNegocioActualizarDTO());

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, ok.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<DireccionSocioNegocioDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1")).ReturnsAsync(respuesta);

            var resultado = await _controller.Eliminar("D1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1"))
                .ReturnsAsync(new Respuesta<DireccionSocioNegocioDTO> { Resultado = true, Dato = new DireccionSocioNegocioDTO { Direccion = "D1", CodigoSn = "SN1" } });
            var respuestaDelete = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.EliminarAsync("D1")).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.Eliminar("D1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, badRequest.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1"))
                .ReturnsAsync(new Respuesta<DireccionSocioNegocioDTO> { Resultado = true, Dato = new DireccionSocioNegocioDTO { Direccion = "D1", CodigoSn = "SN1" } });
            var respuestaDelete = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.EliminarAsync("D1")).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.Eliminar("D1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, ok.Value);
        }
    }
}
