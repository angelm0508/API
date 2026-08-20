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
            _applicationMock.Setup(a => a.ObtenerAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDetDTO> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Obtener("ND1");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Obtener_DevuelveNotFound_CuandoDatoEsNulo()
        {
            _applicationMock.Setup(a => a.ObtenerAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDetDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.Obtener("ND1");

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Obtener_DevuelveOk_CuandoExiste()
        {
            var dto = new NumeracionDocumentoDetDTO { CodigoObj = "ND1", Serie = 1, NombreSerie = "Serie 1", SubTipoDoc = "F" };
            _applicationMock.Setup(a => a.ObtenerAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDetDTO> { Resultado = true, Dato = dto });

            var resultado = await _controller.Obtener("ND1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(dto, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerTodoAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<NumeracionDocumentoDetDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerTodoAsync();

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<NumeracionDocumentoDetDTO> { new NumeracionDocumentoDetDTO { CodigoObj = "ND1", Serie = 1, NombreSerie = "Serie 1", SubTipoDoc = "F" } };
            _applicationMock.Setup(a => a.ObtenerTodoAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<NumeracionDocumentoDetDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerTodoAsync();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new NumeracionDocumentoDetCrearDTO { CodigoObj = "ND1", Serie = 1, NombreSerie = "Serie 1", SubTipoDoc = "F" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<string> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.InsertarAsync(crearDto);

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new NumeracionDocumentoDetCrearDTO { CodigoObj = "ND1", Serie = 1, NombreSerie = "Serie 1", SubTipoDoc = "F" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<string> { Resultado = true, Dato = "ND1" });

            var resultado = await _controller.InsertarAsync(crearDto);

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            _applicationMock.Setup(a => a.ObtenerAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDetDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.ActualizarAsync("ND1", new NumeracionDocumentoDetActualizarDTO());

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDetDTO> { Resultado = true, Dato = new NumeracionDocumentoDetDTO { CodigoObj = "ND1", Serie = 1, NombreSerie = "Serie 1", SubTipoDoc = "F" } });
            _applicationMock.Setup(a => a.ActualizarAsync("ND1", It.IsAny<NumeracionDocumentoDetActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ActualizarAsync("ND1", new NumeracionDocumentoDetActualizarDTO());

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDetDTO> { Resultado = true, Dato = new NumeracionDocumentoDetDTO { CodigoObj = "ND1", Serie = 1, NombreSerie = "Serie 1", SubTipoDoc = "F" } });
            _applicationMock.Setup(a => a.ActualizarAsync("ND1", It.IsAny<NumeracionDocumentoDetActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.ActualizarAsync("ND1", new NumeracionDocumentoDetActualizarDTO());

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            _applicationMock.Setup(a => a.ObtenerAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDetDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.EliminarAsync("ND1");

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDetDTO> { Resultado = true, Dato = new NumeracionDocumentoDetDTO { CodigoObj = "ND1", Serie = 1, NombreSerie = "Serie 1", SubTipoDoc = "F" } });
            _applicationMock.Setup(a => a.EliminarAsync("ND1"))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.EliminarAsync("ND1");

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDetDTO> { Resultado = true, Dato = new NumeracionDocumentoDetDTO { CodigoObj = "ND1", Serie = 1, NombreSerie = "Serie 1", SubTipoDoc = "F" } });
            _applicationMock.Setup(a => a.EliminarAsync("ND1"))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.EliminarAsync("ND1");

            Assert.IsType<OkResult>(resultado);
        }
    }
}
