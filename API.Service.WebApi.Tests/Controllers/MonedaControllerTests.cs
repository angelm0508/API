using API.Application.DTO;
using API.Application.DTO.moneda;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class MonedaControllerTests
    {
        private readonly Mock<IMonedaApplication> _applicationMock;
        private readonly MonedaController _controller;

        public MonedaControllerTests()
        {
            _applicationMock = new Mock<IMonedaApplication>();
            _controller = new MonedaController(_applicationMock.Object);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<MonedaDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("M1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("M1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<MonedaDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("M1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("M1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<MonedaDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("Código de moneda no encontrado.", valor.Mensaje);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveOk_CuandoExiste()
        {
            var dto = new MonedaDTO { Codigo = "M1", Nombre = "Quetzal" };
            var respuesta = new Respuesta<MonedaDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("M1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("M1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<MonedaDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Quetzal")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorNombre("Quetzal");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<MonedaDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Quetzal")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorNombre("Quetzal");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<MonedaDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("Nombre de moneda no encontrado.", valor.Mensaje);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveOk_CuandoExiste()
        {
            var dto = new MonedaDTO { Codigo = "M1", Nombre = "Quetzal" };
            var respuesta = new Respuesta<MonedaDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Quetzal")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorNombre("Quetzal");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<MonedaDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Que")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaNombre("Que");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerContengaNombre_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<MonedaDTO> { new MonedaDTO { Codigo = "M1" } };
            var respuesta = new Respuesta<IEnumerable<MonedaDTO>> { Resultado = true, Dato = datos };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Que")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaNombre("Que");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<MonedaDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("M")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaCodigo("M");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<MonedaDTO> { new MonedaDTO { Codigo = "M1" } };
            var respuesta = new Respuesta<IEnumerable<MonedaDTO>> { Resultado = true, Dato = datos };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("M")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaCodigo("M");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<MonedaDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodo();

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<MonedaDTO> { new MonedaDTO { Codigo = "M1" } };
            var respuesta = new Respuesta<IEnumerable<MonedaDTO>> { Resultado = true, Dato = datos };
            _applicationMock.Setup(a => a.ObtenerAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodo();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task Crear_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new MonedaCrearDTO { Codigo = "M1" };
            var respuesta = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.Crear(crearDto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task Crear_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new MonedaCrearDTO { Codigo = "M1" };
            var respuesta = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.Crear(crearDto);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<MonedaDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("M1")).ReturnsAsync(respuesta);

            var resultado = await _controller.Actualizar("M1", new MonedaActualizarDTO());

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("M1"))
                .ReturnsAsync(new Respuesta<MonedaDTO> { Resultado = true, Dato = new MonedaDTO { Codigo = "M1" } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ActualizarAsync("M1", It.IsAny<MonedaActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.Actualizar("M1", new MonedaActualizarDTO());

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, badRequest.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("M1"))
                .ReturnsAsync(new Respuesta<MonedaDTO> { Resultado = true, Dato = new MonedaDTO { Codigo = "M1" } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.ActualizarAsync("M1", It.IsAny<MonedaActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.Actualizar("M1", new MonedaActualizarDTO());

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, ok.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<MonedaDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("M1")).ReturnsAsync(respuesta);

            var resultado = await _controller.Eliminar("M1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("M1"))
                .ReturnsAsync(new Respuesta<MonedaDTO> { Resultado = true, Dato = new MonedaDTO { Codigo = "M1" } });
            var respuestaDelete = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.EliminarAsync("M1")).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.Eliminar("M1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, badRequest.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("M1"))
                .ReturnsAsync(new Respuesta<MonedaDTO> { Resultado = true, Dato = new MonedaDTO { Codigo = "M1" } });
            var respuestaDelete = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.EliminarAsync("M1")).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.Eliminar("M1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, ok.Value);
        }
    }
}
