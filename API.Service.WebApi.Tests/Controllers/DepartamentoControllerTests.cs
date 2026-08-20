using API.Application.DTO;
using API.Application.DTO.departamento;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class DepartamentoControllerTests
    {
        private readonly Mock<IDepartamentoApplication> _applicationMock;
        private readonly DepartamentoController _controller;

        public DepartamentoControllerTests()
        {
            _applicationMock = new Mock<IDepartamentoApplication>();
            _controller = new DepartamentoController(_applicationMock.Object);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<DepartamentoDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("D1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<DepartamentoDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("D1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<DepartamentoDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("Código de departamento no encontrado.", valor.Mensaje);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveOk_CuandoExiste()
        {
            var dto = new DepartamentoDTO { Codigo = "D1", Pais = "P1", Nombre = "Departamento 1" };
            var respuesta = new Respuesta<DepartamentoDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("D1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<DepartamentoDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Departamento 1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorNombre("Departamento 1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<DepartamentoDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Departamento 1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorNombre("Departamento 1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<DepartamentoDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("Nombre de departamento no encontrado.", valor.Mensaje);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveOk_CuandoExiste()
        {
            var dto = new DepartamentoDTO { Codigo = "D1", Pais = "P1", Nombre = "Departamento 1" };
            var respuesta = new Respuesta<DepartamentoDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Departamento 1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorNombre("Departamento 1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<DepartamentoDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Dep")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaNombre("Dep");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerContengaNombre_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<DepartamentoDTO>> { Resultado = true, Dato = new List<DepartamentoDTO> { new DepartamentoDTO { Codigo = "D1", Pais = "P1" } } };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Dep")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaNombre("Dep");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<DepartamentoDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("D")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaCodigo("D");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<DepartamentoDTO>> { Resultado = true, Dato = new List<DepartamentoDTO> { new DepartamentoDTO { Codigo = "D1", Pais = "P1" } } };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("D")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaCodigo("D");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<DepartamentoDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodo();

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<DepartamentoDTO>> { Resultado = true, Dato = new List<DepartamentoDTO> { new DepartamentoDTO { Codigo = "D1", Pais = "P1" } } };
            _applicationMock.Setup(a => a.ObtenerAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodo();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task Crear_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new DepartamentoCrearDTO { Codigo = "D1", Pais = "P1" };
            var respuesta = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.Crear(crearDto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task Crear_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new DepartamentoCrearDTO { Codigo = "D1", Pais = "P1" };
            var respuesta = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.Crear(crearDto);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<DepartamentoDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1")).ReturnsAsync(respuesta);

            var resultado = await _controller.Actualizar("D1", new DepartamentoActualizarDTO());

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1"))
                .ReturnsAsync(new Respuesta<DepartamentoDTO> { Resultado = true, Dato = new DepartamentoDTO { Codigo = "D1", Pais = "P1" } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ActualizarAsync("D1", It.IsAny<DepartamentoActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.Actualizar("D1", new DepartamentoActualizarDTO());

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, badRequest.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1"))
                .ReturnsAsync(new Respuesta<DepartamentoDTO> { Resultado = true, Dato = new DepartamentoDTO { Codigo = "D1", Pais = "P1" } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.ActualizarAsync("D1", It.IsAny<DepartamentoActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.Actualizar("D1", new DepartamentoActualizarDTO());

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, ok.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<DepartamentoDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1")).ReturnsAsync(respuesta);

            var resultado = await _controller.Eliminar("D1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1"))
                .ReturnsAsync(new Respuesta<DepartamentoDTO> { Resultado = true, Dato = new DepartamentoDTO { Codigo = "D1", Pais = "P1" } });
            var respuestaDelete = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.EliminarAsync("D1")).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.Eliminar("D1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, badRequest.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("D1"))
                .ReturnsAsync(new Respuesta<DepartamentoDTO> { Resultado = true, Dato = new DepartamentoDTO { Codigo = "D1", Pais = "P1" } });
            var respuestaDelete = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.EliminarAsync("D1")).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.Eliminar("D1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, ok.Value);
        }
    }
}
