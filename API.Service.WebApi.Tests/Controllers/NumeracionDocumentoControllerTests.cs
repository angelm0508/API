using API.Application.DTO;
using API.Application.DTO.numeracionDocumento;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class NumeracionDocumentoControllerTests
    {
        private readonly Mock<INumeracionDocumentoApplication> _applicationMock;
        private readonly NumeracionDocumentoController _controller;

        public NumeracionDocumentoControllerTests()
        {
            _applicationMock = new Mock<INumeracionDocumentoApplication>();
            _controller = new NumeracionDocumentoController(_applicationMock.Object);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<NumeracionDocumentoDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("ND1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("ND1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<NumeracionDocumentoDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("ND1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("ND1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<NumeracionDocumentoDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("Código de numeración no encontrado.", valor.Mensaje);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveOk_CuandoExiste()
        {
            var dto = new NumeracionDocumentoDTO { CodigoObj = "ND1", SubTipoDoc = "F" };
            var respuesta = new Respuesta<NumeracionDocumentoDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("ND1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("ND1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<NumeracionDocumentoDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("N")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaCodigo("N");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<NumeracionDocumentoDTO> { new NumeracionDocumentoDTO { CodigoObj = "ND1", SubTipoDoc = "F" } };
            var respuesta = new Respuesta<IEnumerable<NumeracionDocumentoDTO>> { Resultado = true, Dato = datos };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("N")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaCodigo("N");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<NumeracionDocumentoDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodo();

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<NumeracionDocumentoDTO> { new NumeracionDocumentoDTO { CodigoObj = "ND1", SubTipoDoc = "F" } };
            var respuesta = new Respuesta<IEnumerable<NumeracionDocumentoDTO>> { Resultado = true, Dato = datos };
            _applicationMock.Setup(a => a.ObtenerAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodo();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task Crear_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new NumeracionDocumentoCrearDTO { CodigoObj = "ND1", SubTipoDoc = "F" };
            var respuesta = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.Crear(crearDto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task Crear_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new NumeracionDocumentoCrearDTO { CodigoObj = "ND1", SubTipoDoc = "F" };
            var respuesta = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.Crear(crearDto);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<NumeracionDocumentoDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("ND1")).ReturnsAsync(respuesta);

            var resultado = await _controller.Actualizar("ND1", new NumeracionDocumentoActualizarDTO());

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDTO> { Resultado = true, Dato = new NumeracionDocumentoDTO { CodigoObj = "ND1", SubTipoDoc = "F" } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ActualizarAsync("ND1", It.IsAny<NumeracionDocumentoActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.Actualizar("ND1", new NumeracionDocumentoActualizarDTO());

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, badRequest.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDTO> { Resultado = true, Dato = new NumeracionDocumentoDTO { CodigoObj = "ND1", SubTipoDoc = "F" } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.ActualizarAsync("ND1", It.IsAny<NumeracionDocumentoActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.Actualizar("ND1", new NumeracionDocumentoActualizarDTO());

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, ok.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<NumeracionDocumentoDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("ND1")).ReturnsAsync(respuesta);

            var resultado = await _controller.Eliminar("ND1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDTO> { Resultado = true, Dato = new NumeracionDocumentoDTO { CodigoObj = "ND1", SubTipoDoc = "F" } });
            var respuestaDelete = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.EliminarAsync("ND1")).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.Eliminar("ND1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, badRequest.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDTO> { Resultado = true, Dato = new NumeracionDocumentoDTO { CodigoObj = "ND1", SubTipoDoc = "F" } });
            var respuestaDelete = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.EliminarAsync("ND1")).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.Eliminar("ND1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, ok.Value);
        }
    }
}
