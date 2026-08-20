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
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1"))
                .ReturnsAsync(new Respuesta<DireccionSocioNegocioDTO> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerPorCodigo("D1");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveNotFound_CuandoDatoEsNulo()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1"))
                .ReturnsAsync(new Respuesta<DireccionSocioNegocioDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.ObtenerPorCodigo("D1");

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveOk_CuandoExiste()
        {
            var dto = new DireccionSocioNegocioDTO { Direccion = "D1", CodigoSn = "SN1" };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1"))
                .ReturnsAsync(new Respuesta<DireccionSocioNegocioDTO> { Resultado = true, Dato = dto });

            var resultado = await _controller.ObtenerPorCodigo("D1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(dto, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("D"))
                .ReturnsAsync(new Respuesta<IEnumerable<DireccionSocioNegocioDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerContengaCodigo("D");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<DireccionSocioNegocioDTO> { new DireccionSocioNegocioDTO { Direccion = "D1", CodigoSn = "SN1" } };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("D"))
                .ReturnsAsync(new Respuesta<IEnumerable<DireccionSocioNegocioDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerContengaCodigo("D");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<DireccionSocioNegocioDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerTodo();

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<DireccionSocioNegocioDTO> { new DireccionSocioNegocioDTO { Direccion = "D1", CodigoSn = "SN1" } };
            _applicationMock.Setup(a => a.ObtenerAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<DireccionSocioNegocioDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerTodo();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task Crear_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new DireccionSocioNegocioCrearDTO { Direccion = "D1", CodigoSn = "SN1" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Crear(crearDto);

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Crear_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new DireccionSocioNegocioCrearDTO { Direccion = "D1", CodigoSn = "SN1" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Crear(crearDto);

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task Actualizar_DevuelveNotFound_CuandoNoExiste()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1"))
                .ReturnsAsync(new Respuesta<DireccionSocioNegocioDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.Actualizar("D1", new DireccionSocioNegocioActualizarDTO());

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task Actualizar_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1"))
                .ReturnsAsync(new Respuesta<DireccionSocioNegocioDTO> { Resultado = true, Dato = new DireccionSocioNegocioDTO { Direccion = "D1", CodigoSn = "SN1" } });
            _applicationMock.Setup(a => a.ActualizarAsync("D1", It.IsAny<DireccionSocioNegocioActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Actualizar("D1", new DireccionSocioNegocioActualizarDTO());

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Actualizar_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1"))
                .ReturnsAsync(new Respuesta<DireccionSocioNegocioDTO> { Resultado = true, Dato = new DireccionSocioNegocioDTO { Direccion = "D1", CodigoSn = "SN1" } });
            _applicationMock.Setup(a => a.ActualizarAsync("D1", It.IsAny<DireccionSocioNegocioActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Actualizar("D1", new DireccionSocioNegocioActualizarDTO());

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task Eliminar_DevuelveNotFound_CuandoNoExiste()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1"))
                .ReturnsAsync(new Respuesta<DireccionSocioNegocioDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.Eliminar("D1");

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task Eliminar_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1"))
                .ReturnsAsync(new Respuesta<DireccionSocioNegocioDTO> { Resultado = true, Dato = new DireccionSocioNegocioDTO { Direccion = "D1", CodigoSn = "SN1" } });
            _applicationMock.Setup(a => a.EliminarAsync("D1"))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Eliminar("D1");

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Eliminar_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1"))
                .ReturnsAsync(new Respuesta<DireccionSocioNegocioDTO> { Resultado = true, Dato = new DireccionSocioNegocioDTO { Direccion = "D1", CodigoSn = "SN1" } });
            _applicationMock.Setup(a => a.EliminarAsync("D1"))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Eliminar("D1");

            Assert.IsType<OkResult>(resultado);
        }
    }
}
