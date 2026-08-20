using API.Application.DTO;
using API.Application.DTO.numeracion.numeracion_documento_det;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class NumeracionDocumentoDetControllerTests
    {
        private readonly Mock<INumeracionDocumentoDetApplication> _applicationMock;
        private readonly NumeracionDocumentoDetController _controller;

        public NumeracionDocumentoDetControllerTests()
        {
            _applicationMock = new Mock<INumeracionDocumentoDetApplication>();
            _controller = new NumeracionDocumentoDetController(_applicationMock.Object);
        }

        [Fact]
        public async Task Obtener_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<NumeracionDocumentoDetDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerAsync("ND1")).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener("ND1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task Obtener_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<NumeracionDocumentoDetDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync("ND1")).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener("ND1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<NumeracionDocumentoDetDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("El código del documento de numeración no se encontró.", valor.Mensaje);
        }

        [Fact]
        public async Task Obtener_DevuelveOk_CuandoExiste()
        {
            var dto = new NumeracionDocumentoDetDTO { CodigoObj = "ND1", Serie = 1, NombreSerie = "Serie 1", SubTipoDoc = "F" };
            var respuesta = new Respuesta<NumeracionDocumentoDetDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerAsync("ND1")).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener("ND1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<NumeracionDocumentoDetDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerTodoAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodoAsync();

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<NumeracionDocumentoDetDTO> { new NumeracionDocumentoDetDTO { CodigoObj = "ND1", Serie = 1, NombreSerie = "Serie 1", SubTipoDoc = "F" } };
            var respuesta = new Respuesta<IEnumerable<NumeracionDocumentoDetDTO>> { Resultado = true, Dato = datos };
            _applicationMock.Setup(a => a.ObtenerTodoAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodoAsync();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new NumeracionDocumentoDetCrearDTO { CodigoObj = "ND1", Serie = 1, NombreSerie = "Serie 1", SubTipoDoc = "F" };
            var respuesta = new Respuesta<string> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.InsertarAsync(crearDto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new NumeracionDocumentoDetCrearDTO { CodigoObj = "ND1", Serie = 1, NombreSerie = "Serie 1", SubTipoDoc = "F" };
            var respuesta = new Respuesta<string> { Resultado = true, Dato = "ND1" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.InsertarAsync(crearDto);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<NumeracionDocumentoDetDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync("ND1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ActualizarAsync("ND1", new NumeracionDocumentoDetActualizarDTO());

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDetDTO> { Resultado = true, Dato = new NumeracionDocumentoDetDTO { CodigoObj = "ND1", Serie = 1, NombreSerie = "Serie 1", SubTipoDoc = "F" } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ActualizarAsync("ND1", It.IsAny<NumeracionDocumentoDetActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.ActualizarAsync("ND1", new NumeracionDocumentoDetActualizarDTO());

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, badRequest.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDetDTO> { Resultado = true, Dato = new NumeracionDocumentoDetDTO { CodigoObj = "ND1", Serie = 1, NombreSerie = "Serie 1", SubTipoDoc = "F" } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.ActualizarAsync("ND1", It.IsAny<NumeracionDocumentoDetActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.ActualizarAsync("ND1", new NumeracionDocumentoDetActualizarDTO());

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, ok.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<NumeracionDocumentoDetDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync("ND1")).ReturnsAsync(respuesta);

            var resultado = await _controller.EliminarAsync("ND1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDetDTO> { Resultado = true, Dato = new NumeracionDocumentoDetDTO { CodigoObj = "ND1", Serie = 1, NombreSerie = "Serie 1", SubTipoDoc = "F" } });
            var respuestaDelete = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.EliminarAsync("ND1")).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.EliminarAsync("ND1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, badRequest.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDetDTO> { Resultado = true, Dato = new NumeracionDocumentoDetDTO { CodigoObj = "ND1", Serie = 1, NombreSerie = "Serie 1", SubTipoDoc = "F" } });
            var respuestaDelete = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.EliminarAsync("ND1")).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.EliminarAsync("ND1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, ok.Value);
        }
    }
}
