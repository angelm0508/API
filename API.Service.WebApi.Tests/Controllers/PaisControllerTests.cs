using API.Application.DTO;
using API.Application.DTO.pais;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class PaisControllerTests
    {
        private readonly Mock<IPaisApplication> _applicationMock;
        private readonly PaisController _controller;

        public PaisControllerTests()
        {
            _applicationMock = new Mock<IPaisApplication>();
            _controller = new PaisController(_applicationMock.Object);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<PaisDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("GT")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("GT");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<PaisDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("GT")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("GT");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<PaisDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("Código de país no encontrado.", valor.Mensaje);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveOk_CuandoExiste()
        {
            var dto = new PaisDTO { Codigo = "GT", Nombre = "Guatemala" };
            var respuesta = new Respuesta<PaisDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("GT")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("GT");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<PaisDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Guatemala")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorNombre("Guatemala");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<PaisDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Guatemala")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorNombre("Guatemala");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<PaisDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("Nombre de país no encontrado.", valor.Mensaje);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveOk_CuandoExiste()
        {
            var dto = new PaisDTO { Codigo = "GT", Nombre = "Guatemala" };
            var respuesta = new Respuesta<PaisDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Guatemala")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorNombre("Guatemala");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<PaisDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Guat")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaNombre("Guat");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerContengaNombre_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<PaisDTO> { new PaisDTO { Codigo = "GT" } };
            var respuesta = new Respuesta<IEnumerable<PaisDTO>> { Resultado = true, Dato = datos };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Guat")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaNombre("Guat");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<PaisDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("G")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaCodigo("G");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<PaisDTO> { new PaisDTO { Codigo = "GT" } };
            var respuesta = new Respuesta<IEnumerable<PaisDTO>> { Resultado = true, Dato = datos };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("G")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaCodigo("G");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<PaisDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodo();

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<PaisDTO> { new PaisDTO { Codigo = "GT" } };
            var respuesta = new Respuesta<IEnumerable<PaisDTO>> { Resultado = true, Dato = datos };
            _applicationMock.Setup(a => a.ObtenerAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodo();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task Crear_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new PaisCrearDTO { Codigo = "GT" };
            var respuesta = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.Crear(crearDto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task Crear_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new PaisCrearDTO { Codigo = "GT" };
            var respuesta = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.Crear(crearDto);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<PaisDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("GT")).ReturnsAsync(respuesta);

            var resultado = await _controller.Actualizar("GT", new PaisActualizarDTO());

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("GT"))
                .ReturnsAsync(new Respuesta<PaisDTO> { Resultado = true, Dato = new PaisDTO { Codigo = "GT" } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ActualizarAsync("GT", It.IsAny<PaisActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.Actualizar("GT", new PaisActualizarDTO());

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, badRequest.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("GT"))
                .ReturnsAsync(new Respuesta<PaisDTO> { Resultado = true, Dato = new PaisDTO { Codigo = "GT" } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.ActualizarAsync("GT", It.IsAny<PaisActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.Actualizar("GT", new PaisActualizarDTO());

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, ok.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<PaisDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("GT")).ReturnsAsync(respuesta);

            var resultado = await _controller.Eliminar("GT");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("GT"))
                .ReturnsAsync(new Respuesta<PaisDTO> { Resultado = true, Dato = new PaisDTO { Codigo = "GT" } });
            var respuestaDelete = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.EliminarAsync("GT")).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.Eliminar("GT");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, badRequest.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("GT"))
                .ReturnsAsync(new Respuesta<PaisDTO> { Resultado = true, Dato = new PaisDTO { Codigo = "GT" } });
            var respuestaDelete = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.EliminarAsync("GT")).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.Eliminar("GT");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, ok.Value);
        }
    }
}
