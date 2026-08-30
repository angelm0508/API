using API.Application.DTO;
using API.Application.DTO.cotizacion;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class CotizacionDetalleControllerTests
    {
        private readonly Mock<ICotizacionDetalleApplication> _applicationMock;
        private readonly CotizacionDetalleController _controller;

        public CotizacionDetalleControllerTests()
        {
            _applicationMock = new Mock<ICotizacionDetalleApplication>();
            _controller = new CotizacionDetalleController(_applicationMock.Object);
        }

        [Fact]
        public async Task Obtener_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<CotizacionDetalleDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1, 1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task Obtener_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<CotizacionDetalleDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1, 1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<CotizacionDetalleDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
        }

        [Fact]
        public async Task Obtener_DevuelveOk_CuandoExiste()
        {
            var dto = new CotizacionDetalleDTO { Entry = 1, NoLinea = 1, CodArticulo = "ART1" };
            var respuesta = new Respuesta<CotizacionDetalleDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1, 1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorCotizacion_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<CotizacionDetalleDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerPorCotizacionAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCotizacion(1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerPorCotizacion_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<CotizacionDetalleDTO>>
            {
                Resultado = true,
                Dato = new List<CotizacionDetalleDTO> { new CotizacionDetalleDTO { Entry = 1, NoLinea = 1 } }
            };
            _applicationMock.Setup(a => a.ObtenerPorCotizacionAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCotizacion(1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<CotizacionDetalleDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerTodoAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodoAsync();

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<CotizacionDetalleDTO>>
            {
                Resultado = true,
                Dato = new List<CotizacionDetalleDTO> { new CotizacionDetalleDTO { Entry = 1, NoLinea = 1 } }
            };
            _applicationMock.Setup(a => a.ObtenerTodoAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodoAsync();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new CotizacionDetalleCrearDTO { Entry = 1, CodArticulo = "ART1" };
            var respuesta = new Respuesta<int> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.InsertarAsync(crearDto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new CotizacionDetalleCrearDTO { Entry = 1, CodArticulo = "ART1" };
            var respuesta = new Respuesta<int> { Resultado = true, Dato = 1 };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.InsertarAsync(crearDto);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<CotizacionDetalleDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.ActualizarAsync(1, 1, new CotizacionDetalleActualizarDTO());

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1))
                .ReturnsAsync(new Respuesta<CotizacionDetalleDTO> { Resultado = true, Dato = new CotizacionDetalleDTO { Entry = 1, NoLinea = 1 } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ActualizarAsync(1, 1, It.IsAny<CotizacionDetalleActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.ActualizarAsync(1, 1, new CotizacionDetalleActualizarDTO());

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, badRequest.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1))
                .ReturnsAsync(new Respuesta<CotizacionDetalleDTO> { Resultado = true, Dato = new CotizacionDetalleDTO { Entry = 1, NoLinea = 1 } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.ActualizarAsync(1, 1, It.IsAny<CotizacionDetalleActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.ActualizarAsync(1, 1, new CotizacionDetalleActualizarDTO());

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, ok.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<CotizacionDetalleDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.EliminarAsync(1, 1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1))
                .ReturnsAsync(new Respuesta<CotizacionDetalleDTO> { Resultado = true, Dato = new CotizacionDetalleDTO { Entry = 1, NoLinea = 1 } });
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
                .ReturnsAsync(new Respuesta<CotizacionDetalleDTO> { Resultado = true, Dato = new CotizacionDetalleDTO { Entry = 1, NoLinea = 1 } });
            var respuestaDelete = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.EliminarAsync(1, 1)).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.EliminarAsync(1, 1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, ok.Value);
        }
    }
}
