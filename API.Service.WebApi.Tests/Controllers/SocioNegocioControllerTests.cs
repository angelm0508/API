using API.Application.DTO;
using API.Application.DTO.socioNegocio;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class SocioNegocioControllerTests
    {
        private readonly Mock<ISocioNegocioApplication> _applicationMock;
        private readonly SocioNegocioController _controller;

        public SocioNegocioControllerTests()
        {
            _applicationMock = new Mock<ISocioNegocioApplication>();
            _controller = new SocioNegocioController(_applicationMock.Object);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("SN1"))
                .ReturnsAsync(new Respuesta<SocioNegocioDTO> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerPorCodigo("SN1");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveNotFound_CuandoDatoEsNulo()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("SN1"))
                .ReturnsAsync(new Respuesta<SocioNegocioDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.ObtenerPorCodigo("SN1");

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveOk_CuandoExiste()
        {
            var dto = new SocioNegocioDTO { Codigo = "SN1", Nombre = "Cliente 1" };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("SN1"))
                .ReturnsAsync(new Respuesta<SocioNegocioDTO> { Resultado = true, Dato = dto });

            var resultado = await _controller.ObtenerPorCodigo("SN1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(dto, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Cliente 1"))
                .ReturnsAsync(new Respuesta<SocioNegocioDTO> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerPorNombre("Cliente 1");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveNotFound_CuandoDatoEsNulo()
        {
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Cliente 1"))
                .ReturnsAsync(new Respuesta<SocioNegocioDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.ObtenerPorNombre("Cliente 1");

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveOk_CuandoExiste()
        {
            var dto = new SocioNegocioDTO { Codigo = "SN1", Nombre = "Cliente 1" };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Cliente 1"))
                .ReturnsAsync(new Respuesta<SocioNegocioDTO> { Resultado = true, Dato = dto });

            var resultado = await _controller.ObtenerPorNombre("Cliente 1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(dto, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Clie"))
                .ReturnsAsync(new Respuesta<IEnumerable<SocioNegocioDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerContengaNombre("Clie");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerContengaNombre_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<SocioNegocioDTO> { new SocioNegocioDTO { Codigo = "SN1" } };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Clie"))
                .ReturnsAsync(new Respuesta<IEnumerable<SocioNegocioDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerContengaNombre("Clie");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("S"))
                .ReturnsAsync(new Respuesta<IEnumerable<SocioNegocioDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerContengaCodigo("S");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<SocioNegocioDTO> { new SocioNegocioDTO { Codigo = "SN1" } };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("S"))
                .ReturnsAsync(new Respuesta<IEnumerable<SocioNegocioDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerContengaCodigo("S");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<SocioNegocioDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerTodo();

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<SocioNegocioDTO> { new SocioNegocioDTO { Codigo = "SN1" } };
            _applicationMock.Setup(a => a.ObtenerAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<SocioNegocioDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerTodo();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task Crear_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new SocioNegocioCrearDTO { Codigo = "SN1" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Crear(crearDto);

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Crear_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new SocioNegocioCrearDTO { Codigo = "SN1" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Crear(crearDto);

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task Actualizar_DevuelveNotFound_CuandoNoExiste()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("SN1"))
                .ReturnsAsync(new Respuesta<SocioNegocioDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.Actualizar("SN1", new SocioNegocioActualizarDTO());

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task Actualizar_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("SN1"))
                .ReturnsAsync(new Respuesta<SocioNegocioDTO> { Resultado = true, Dato = new SocioNegocioDTO { Codigo = "SN1" } });
            _applicationMock.Setup(a => a.ActualizarAsync("SN1", It.IsAny<SocioNegocioActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Actualizar("SN1", new SocioNegocioActualizarDTO());

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Actualizar_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("SN1"))
                .ReturnsAsync(new Respuesta<SocioNegocioDTO> { Resultado = true, Dato = new SocioNegocioDTO { Codigo = "SN1" } });
            _applicationMock.Setup(a => a.ActualizarAsync("SN1", It.IsAny<SocioNegocioActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Actualizar("SN1", new SocioNegocioActualizarDTO());

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task Eliminar_DevuelveNotFound_CuandoNoExiste()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("SN1"))
                .ReturnsAsync(new Respuesta<SocioNegocioDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.Eliminar("SN1");

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task Eliminar_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("SN1"))
                .ReturnsAsync(new Respuesta<SocioNegocioDTO> { Resultado = true, Dato = new SocioNegocioDTO { Codigo = "SN1" } });
            _applicationMock.Setup(a => a.EliminarAsync("SN1"))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Eliminar("SN1");

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Eliminar_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("SN1"))
                .ReturnsAsync(new Respuesta<SocioNegocioDTO> { Resultado = true, Dato = new SocioNegocioDTO { Codigo = "SN1" } });
            _applicationMock.Setup(a => a.EliminarAsync("SN1"))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Eliminar("SN1");

            Assert.IsType<OkResult>(resultado);
        }
    }
}
