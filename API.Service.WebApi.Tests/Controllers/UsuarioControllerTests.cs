using API.Application.DTO;
using API.Application.DTO.usuario.usuario;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class UsuarioControllerTests
    {
        private readonly Mock<IUsuarioApplication> _applicationMock;
        private readonly UsuarioController _controller;

        public UsuarioControllerTests()
        {
            _applicationMock = new Mock<IUsuarioApplication>();
            _controller = new UsuarioController(_applicationMock.Object);
        }

        [Fact]
        public async Task Obtener_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<UsuarioDTO> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Obtener(1);

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Obtener_DevuelveNotFound_CuandoDatoEsNulo()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<UsuarioDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.Obtener(1);

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Obtener_DevuelveOk_CuandoExiste()
        {
            var dto = new UsuarioDTO { Id = 1, Codigo = "U1" };
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<UsuarioDTO> { Resultado = true, Dato = dto });

            var resultado = await _controller.Obtener(1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(dto, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerAsync("U1"))
                .ReturnsAsync(new Respuesta<UsuarioDTO> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerPorCodigo("U1");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveNotFound_CuandoDatoEsNulo()
        {
            _applicationMock.Setup(a => a.ObtenerAsync("U1"))
                .ReturnsAsync(new Respuesta<UsuarioDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.ObtenerPorCodigo("U1");

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveOk_CuandoExiste()
        {
            var dto = new UsuarioDTO { Id = 1, Codigo = "U1" };
            _applicationMock.Setup(a => a.ObtenerAsync("U1"))
                .ReturnsAsync(new Respuesta<UsuarioDTO> { Resultado = true, Dato = dto });

            var resultado = await _controller.ObtenerPorCodigo("U1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(dto, ok.Value);
        }

        [Fact]
        public async Task ObteneContengaNombreAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("U"))
                .ReturnsAsync(new Respuesta<IEnumerable<UsuarioDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObteneContengaNombreAsync("U");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObteneContengaNombreAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<UsuarioDTO> { new UsuarioDTO { Id = 1, Codigo = "U1" } };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("U"))
                .ReturnsAsync(new Respuesta<IEnumerable<UsuarioDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObteneContengaNombreAsync("U");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerTodoAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<UsuarioDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerTodoAsync();

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<UsuarioDTO> { new UsuarioDTO { Id = 1, Codigo = "U1" } };
            _applicationMock.Setup(a => a.ObtenerTodoAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<UsuarioDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerTodoAsync();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new UsuarioCrearDTO { Codigo = "U1" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<int> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.InsertarAsync(crearDto);

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new UsuarioCrearDTO { Codigo = "U1" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<int> { Resultado = true, Dato = 1 });

            var resultado = await _controller.InsertarAsync(crearDto);

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<UsuarioDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.ActualizarAsync(1, new UsuarioActualizarDTO());

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<UsuarioDTO> { Resultado = true, Dato = new UsuarioDTO { Id = 1, Codigo = "U1" } });
            _applicationMock.Setup(a => a.ActualizarAsync(1, It.IsAny<UsuarioActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ActualizarAsync(1, new UsuarioActualizarDTO());

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<UsuarioDTO> { Resultado = true, Dato = new UsuarioDTO { Id = 1, Codigo = "U1" } });
            _applicationMock.Setup(a => a.ActualizarAsync(1, It.IsAny<UsuarioActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.ActualizarAsync(1, new UsuarioActualizarDTO());

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<UsuarioDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.EliminarAsync(1);

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<UsuarioDTO> { Resultado = true, Dato = new UsuarioDTO { Id = 1, Codigo = "U1" } });
            _applicationMock.Setup(a => a.EliminarAsync(1))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.EliminarAsync(1);

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<UsuarioDTO> { Resultado = true, Dato = new UsuarioDTO { Id = 1, Codigo = "U1" } });
            _applicationMock.Setup(a => a.EliminarAsync(1))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.EliminarAsync(1);

            Assert.IsType<OkResult>(resultado);
        }
    }
}
