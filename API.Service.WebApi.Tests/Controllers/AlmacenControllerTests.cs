using API.Application.DTO;
using API.Application.DTO.almacen;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class AlmacenControllerTests
    {
        private readonly Mock<IAlmacenApplication> _applicationMock;
        private readonly AlmacenController _controller;

        public AlmacenControllerTests()
        {
            _applicationMock = new Mock<IAlmacenApplication>();
            _controller = new AlmacenController(_applicationMock.Object);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<AlmacenDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("A1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<AlmacenDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("A1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<AlmacenDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("Código de almacén no encontrado.", valor.Mensaje);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveOk_CuandoExiste()
        {
            var dto = new AlmacenDTO { Codigo = "A1", Nombre = "Bodega", Activo = "S" };
            var respuesta = new Respuesta<AlmacenDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("A1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<AlmacenDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Bodega")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorNombre("Bodega");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<AlmacenDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Bodega")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorNombre("Bodega");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<AlmacenDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("Nombre de almacén no encontrado.", valor.Mensaje);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveOk_CuandoExiste()
        {
            var dto = new AlmacenDTO { Codigo = "A1", Nombre = "Bodega", Activo = "S" };
            var respuesta = new Respuesta<AlmacenDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Bodega")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorNombre("Bodega");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<AlmacenDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Bod")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaNombre("Bod");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerContengaNombre_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<AlmacenDTO>> { Resultado = true, Dato = new List<AlmacenDTO> { new AlmacenDTO { Codigo = "A1", Activo = "S" } } };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Bod")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaNombre("Bod");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<AlmacenDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("A")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaCodigo("A");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<AlmacenDTO>> { Resultado = true, Dato = new List<AlmacenDTO> { new AlmacenDTO { Codigo = "A1", Activo = "S" } } };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("A")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaCodigo("A");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<AlmacenDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodo();

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<AlmacenDTO>> { Resultado = true, Dato = new List<AlmacenDTO> { new AlmacenDTO { Codigo = "A1", Activo = "S" } } };
            _applicationMock.Setup(a => a.ObtenerAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodo();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task Crear_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new AlmacenCrearDTO { Codigo = "A1", Activo = "S" };
            var respuesta = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.Crear(crearDto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task Crear_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new AlmacenCrearDTO { Codigo = "A1", Activo = "S" };
            var respuesta = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.Crear(crearDto);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<AlmacenDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1")).ReturnsAsync(respuesta);

            var resultado = await _controller.Actualizar("A1", new AlmacenActualizarDTO());

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<AlmacenDTO> { Resultado = true, Dato = new AlmacenDTO { Codigo = "A1", Activo = "S" } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ActualizarAsync("A1", It.IsAny<AlmacenActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.Actualizar("A1", new AlmacenActualizarDTO());

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, badRequest.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<AlmacenDTO> { Resultado = true, Dato = new AlmacenDTO { Codigo = "A1", Activo = "S" } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.ActualizarAsync("A1", It.IsAny<AlmacenActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.Actualizar("A1", new AlmacenActualizarDTO());

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, ok.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<AlmacenDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1")).ReturnsAsync(respuesta);

            var resultado = await _controller.Eliminar("A1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<AlmacenDTO> { Resultado = true, Dato = new AlmacenDTO { Codigo = "A1", Activo = "S" } });
            var respuestaDelete = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.EliminarAsync("A1")).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.Eliminar("A1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, badRequest.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<AlmacenDTO> { Resultado = true, Dato = new AlmacenDTO { Codigo = "A1", Activo = "S" } });
            var respuestaDelete = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.EliminarAsync("A1")).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.Eliminar("A1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, ok.Value);
        }
    }
}
