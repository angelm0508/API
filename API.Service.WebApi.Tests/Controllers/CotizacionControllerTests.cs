using API.Application.DTO;
using API.Application.DTO.cotizacion;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class CotizacionControllerTests
    {
        private readonly Mock<ICotizacionApplication> _applicationMock;
        private readonly CotizacionController _controller;

        public CotizacionControllerTests()
        {
            _applicationMock = new Mock<ICotizacionApplication>();
            _controller = new CotizacionController(_applicationMock.Object);
        }

        [Fact]
        public async Task Obtener_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<CotizacionDTO> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Obtener(1);

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Obtener_DevuelveNotFound_CuandoDatoEsNulo()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<CotizacionDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.Obtener(1);

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Obtener_DevuelveOk_CuandoExiste()
        {
            var dto = new CotizacionDTO { Entry = 1, NumDoc = 100, Serie = 1 };
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<CotizacionDTO> { Resultado = true, Dato = dto });

            var resultado = await _controller.Obtener(1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(dto, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerTodoAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<CotizacionDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerTodoAsync();

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<CotizacionDTO> { new CotizacionDTO { Entry = 1 } };
            _applicationMock.Setup(a => a.ObtenerTodoAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<CotizacionDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerTodoAsync();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new CotizacionCrearDTO { NumDoc = 100, Serie = 1 };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<int> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.InsertarAsync(crearDto);

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new CotizacionCrearDTO { NumDoc = 100, Serie = 1 };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<int> { Resultado = true, Dato = 1 });

            var resultado = await _controller.InsertarAsync(crearDto);

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<CotizacionDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.ActualizarAsync(1, new CotizacionActualizarDTO());

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<CotizacionDTO> { Resultado = true, Dato = new CotizacionDTO { Entry = 1 } });
            _applicationMock.Setup(a => a.ActualizarAsync(1, It.IsAny<CotizacionActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ActualizarAsync(1, new CotizacionActualizarDTO());

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<CotizacionDTO> { Resultado = true, Dato = new CotizacionDTO { Entry = 1 } });
            _applicationMock.Setup(a => a.ActualizarAsync(1, It.IsAny<CotizacionActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.ActualizarAsync(1, new CotizacionActualizarDTO());

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<CotizacionDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.EliminarAsync(1);

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<CotizacionDTO> { Resultado = true, Dato = new CotizacionDTO { Entry = 1 } });
            _applicationMock.Setup(a => a.EliminarAsync(1))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.EliminarAsync(1);

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<CotizacionDTO> { Resultado = true, Dato = new CotizacionDTO { Entry = 1 } });
            _applicationMock.Setup(a => a.EliminarAsync(1))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.EliminarAsync(1);

            Assert.IsType<OkResult>(resultado);
        }
    }
}
