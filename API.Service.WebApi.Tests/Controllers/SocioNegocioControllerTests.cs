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
            var respuesta = new Respuesta<SocioNegocioDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("SN1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("SN1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<SocioNegocioDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("SN1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("SN1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<SocioNegocioDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("Código de socio negocio no encontrado.", valor.Mensaje);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveOk_CuandoExiste()
        {
            var dto = new SocioNegocioDTO { Codigo = "SN1", Nombre = "Cliente 1" };
            var respuesta = new Respuesta<SocioNegocioDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("SN1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("SN1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<SocioNegocioDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Cliente 1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorNombre("Cliente 1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<SocioNegocioDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Cliente 1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorNombre("Cliente 1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<SocioNegocioDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("Nombre de socio negocio no encontrado.", valor.Mensaje);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveOk_CuandoExiste()
        {
            var dto = new SocioNegocioDTO { Codigo = "SN1", Nombre = "Cliente 1" };
            var respuesta = new Respuesta<SocioNegocioDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Cliente 1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorNombre("Cliente 1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<SocioNegocioDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Clie")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaNombre("Clie");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerContengaNombre_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<SocioNegocioDTO> { new SocioNegocioDTO { Codigo = "SN1" } };
            var respuesta = new Respuesta<IEnumerable<SocioNegocioDTO>> { Resultado = true, Dato = datos };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Clie")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaNombre("Clie");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<SocioNegocioDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("S")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaCodigo("S");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<SocioNegocioDTO> { new SocioNegocioDTO { Codigo = "SN1" } };
            var respuesta = new Respuesta<IEnumerable<SocioNegocioDTO>> { Resultado = true, Dato = datos };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("S")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaCodigo("S");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<SocioNegocioDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodo();

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<SocioNegocioDTO> { new SocioNegocioDTO { Codigo = "SN1" } };
            var respuesta = new Respuesta<IEnumerable<SocioNegocioDTO>> { Resultado = true, Dato = datos };
            _applicationMock.Setup(a => a.ObtenerAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodo();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task Crear_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new SocioNegocioCrearDTO { Codigo = "SN1" };
            var respuesta = new Respuesta<string> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.Crear(crearDto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task Crear_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new SocioNegocioCrearDTO { Codigo = "SN1" };
            var respuesta = new Respuesta<string> { Resultado = true, Dato = "SN1" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.Crear(crearDto);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<SocioNegocioDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("SN1")).ReturnsAsync(respuesta);

            var resultado = await _controller.Actualizar("SN1", new SocioNegocioActualizarDTO());

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("SN1"))
                .ReturnsAsync(new Respuesta<SocioNegocioDTO> { Resultado = true, Dato = new SocioNegocioDTO { Codigo = "SN1" } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ActualizarAsync("SN1", It.IsAny<SocioNegocioActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.Actualizar("SN1", new SocioNegocioActualizarDTO());

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, badRequest.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("SN1"))
                .ReturnsAsync(new Respuesta<SocioNegocioDTO> { Resultado = true, Dato = new SocioNegocioDTO { Codigo = "SN1" } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.ActualizarAsync("SN1", It.IsAny<SocioNegocioActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.Actualizar("SN1", new SocioNegocioActualizarDTO());

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, ok.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<SocioNegocioDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("SN1")).ReturnsAsync(respuesta);

            var resultado = await _controller.Eliminar("SN1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("SN1"))
                .ReturnsAsync(new Respuesta<SocioNegocioDTO> { Resultado = true, Dato = new SocioNegocioDTO { Codigo = "SN1" } });
            var respuestaDelete = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.EliminarAsync("SN1")).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.Eliminar("SN1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, badRequest.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("SN1"))
                .ReturnsAsync(new Respuesta<SocioNegocioDTO> { Resultado = true, Dato = new SocioNegocioDTO { Codigo = "SN1" } });
            var respuestaDelete = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.EliminarAsync("SN1")).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.Eliminar("SN1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, ok.Value);
        }
    }
}
