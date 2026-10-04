using API.Application.DTO;
using API.Application.DTO.usuario.usuario;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
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
            var respuesta = new Respuesta<UsuarioDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task Obtener_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<UsuarioDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<UsuarioDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("El código del usuario no se encontró.", valor.Mensaje);
        }

        [Fact]
        public async Task Obtener_DevuelveOk_CuandoExiste()
        {
            var dto = new UsuarioDTO { Id = 1, Codigo = "U1" };
            var respuesta = new Respuesta<UsuarioDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<UsuarioDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerAsync("U1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("U1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<UsuarioDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync("U1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("U1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<UsuarioDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("El código del usuario no se encontró.", valor.Mensaje);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveOk_CuandoExiste()
        {
            var dto = new UsuarioDTO { Id = 1, Codigo = "U1" };
            var respuesta = new Respuesta<UsuarioDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerAsync("U1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("U1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObteneContengaNombreAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<UsuarioDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("U")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObteneContengaNombreAsync("U");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObteneContengaNombreAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<UsuarioDTO> { new UsuarioDTO { Id = 1, Codigo = "U1" } };
            var respuesta = new Respuesta<IEnumerable<UsuarioDTO>> { Resultado = true, Dato = datos };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("U")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObteneContengaNombreAsync("U");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<UsuarioDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerTodoAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodoAsync();

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<UsuarioDTO> { new UsuarioDTO { Id = 1, Codigo = "U1" } };
            var respuesta = new Respuesta<IEnumerable<UsuarioDTO>> { Resultado = true, Dato = datos };
            _applicationMock.Setup(a => a.ObtenerTodoAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodoAsync();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new UsuarioCrearDTO { Codigo = "U1" };
            var respuesta = new Respuesta<int> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.InsertarAsync(crearDto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new UsuarioCrearDTO { Codigo = "U1" };
            var respuesta = new Respuesta<int> { Resultado = true, Dato = 1 };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.InsertarAsync(crearDto);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<UsuarioDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.ActualizarAsync(1, new UsuarioActualizarDTO());

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<UsuarioDTO> { Resultado = true, Dato = new UsuarioDTO { Id = 1, Codigo = "U1" } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ActualizarAsync(1, It.IsAny<UsuarioActualizarDTO>(), null)).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.ActualizarAsync(1, new UsuarioActualizarDTO());

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, badRequest.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<UsuarioDTO> { Resultado = true, Dato = new UsuarioDTO { Id = 1, Codigo = "U1" } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.ActualizarAsync(1, It.IsAny<UsuarioActualizarDTO>(), null)).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.ActualizarAsync(1, new UsuarioActualizarDTO());

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, ok.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<UsuarioDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.EliminarAsync(1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<UsuarioDTO> { Resultado = true, Dato = new UsuarioDTO { Id = 1, Codigo = "U1" } });
            var respuestaDelete = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.EliminarAsync(1, null)).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.EliminarAsync(1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, badRequest.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<UsuarioDTO> { Resultado = true, Dato = new UsuarioDTO { Id = 1, Codigo = "U1" } });
            var respuestaDelete = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.EliminarAsync(1, null)).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.EliminarAsync(1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, ok.Value);
        }

        // ---------- Usuario actual (claim NameIdentifier del JWT) ----------

        private UsuarioController ControllerConClaims(params Claim[] claims) =>
            new(_applicationMock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
                }
            };

        private void UsuarioExiste(int id) =>
            _applicationMock.Setup(a => a.ObtenerAsync(id))
                .ReturnsAsync(new Respuesta<UsuarioDTO> { Resultado = true, Dato = new UsuarioDTO { Id = id, Codigo = "U" + id } });

        [Fact]
        public async Task ActualizarAsync_PasaElIdDelClaimNameIdentifierComoUsuarioActual()
        {
            UsuarioExiste(2);
            var dto = new UsuarioActualizarDTO { Nombre = "N" };
            _applicationMock.Setup(a => a.ActualizarAsync(2, dto, 7)).ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });
            var controller = ControllerConClaims(new Claim(ClaimTypes.NameIdentifier, "7"));

            var resultado = await controller.ActualizarAsync(2, dto);

            Assert.IsType<OkObjectResult>(resultado.Result);
            _applicationMock.Verify(a => a.ActualizarAsync(2, dto, 7), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_SinClaim_PasaNuloYNoFalla()
        {
            UsuarioExiste(2);
            var dto = new UsuarioActualizarDTO { Nombre = "N" };
            _applicationMock.Setup(a => a.ActualizarAsync(2, dto, null)).ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });
            var controller = ControllerConClaims();

            var resultado = await controller.ActualizarAsync(2, dto);

            Assert.IsType<OkObjectResult>(resultado.Result);
            _applicationMock.Verify(a => a.ActualizarAsync(2, dto, null), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_ClaimNoNumerico_SeTrataComoSinClaim()
        {
            UsuarioExiste(2);
            var dto = new UsuarioActualizarDTO { Nombre = "N" };
            _applicationMock.Setup(a => a.ActualizarAsync(2, dto, null)).ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });
            var controller = ControllerConClaims(new Claim(ClaimTypes.NameIdentifier, "abc"));

            var resultado = await controller.ActualizarAsync(2, dto);

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ActualizarAsync_ErrorDeAutoProteccion_DevuelveBadRequestConElMensaje()
        {
            UsuarioExiste(2);
            var dto = new UsuarioActualizarDTO { Nombre = "N", Bloqueado = "S" };
            var respuesta = new Respuesta<bool> { Resultado = false, Mensaje = "No puede bloquear su propio usuario." };
            _applicationMock.Setup(a => a.ActualizarAsync(2, dto, 2)).ReturnsAsync(respuesta);
            var controller = ControllerConClaims(new Claim(ClaimTypes.NameIdentifier, "2"));

            var resultado = await controller.ActualizarAsync(2, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task EliminarAsync_PasaElIdDelClaimComoUsuarioActual()
        {
            UsuarioExiste(3);
            _applicationMock.Setup(a => a.EliminarAsync(3, 7)).ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });
            var controller = ControllerConClaims(new Claim(ClaimTypes.NameIdentifier, "7"));

            var resultado = await controller.EliminarAsync(3);

            Assert.IsType<OkObjectResult>(resultado.Result);
            _applicationMock.Verify(a => a.EliminarAsync(3, 7), Times.Once);
        }

        [Fact]
        public async Task EliminarAsync_SinClaim_PasaNulo()
        {
            UsuarioExiste(3);
            _applicationMock.Setup(a => a.EliminarAsync(3, null)).ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });
            var controller = ControllerConClaims();

            var resultado = await controller.EliminarAsync(3);

            Assert.IsType<OkObjectResult>(resultado.Result);
            _applicationMock.Verify(a => a.EliminarAsync(3, null), Times.Once);
        }

        [Fact]
        public async Task EliminarAsync_AutoEliminacion_DevuelveBadRequest()
        {
            UsuarioExiste(7);
            var respuesta = new Respuesta<bool> { Resultado = false, Mensaje = "No puede eliminar su propio usuario." };
            _applicationMock.Setup(a => a.EliminarAsync(7, 7)).ReturnsAsync(respuesta);
            var controller = ControllerConClaims(new Claim(ClaimTypes.NameIdentifier, "7"));

            var resultado = await controller.EliminarAsync(7);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task EliminarAsync_UsuarioYaEliminado_DevuelveNotFound_SinLlamarAEliminar()
        {
            // ObtenerAsync(id) excluye eliminados: un eliminado llega como Dato nulo.
            _applicationMock.Setup(a => a.ObtenerAsync(3)).ReturnsAsync(new Respuesta<UsuarioDTO> { Resultado = true, Dato = null! });
            var controller = ControllerConClaims(new Claim(ClaimTypes.NameIdentifier, "7"));

            var resultado = await controller.EliminarAsync(3);

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
            _applicationMock.Verify(a => a.EliminarAsync(It.IsAny<int>(), It.IsAny<int?>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_UsuarioEliminado_DevuelveNotFound_SinLlamarAActualizar()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(3)).ReturnsAsync(new Respuesta<UsuarioDTO> { Resultado = true, Dato = null! });
            var controller = ControllerConClaims(new Claim(ClaimTypes.NameIdentifier, "7"));

            var resultado = await controller.ActualizarAsync(3, new UsuarioActualizarDTO());

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
            _applicationMock.Verify(a => a.ActualizarAsync(It.IsAny<int>(), It.IsAny<UsuarioActualizarDTO>(), It.IsAny<int?>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveElNuevoIdEnDato()
        {
            var crearDto = new UsuarioCrearDTO { Codigo = "U8" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(new Respuesta<int> { Resultado = true, Dato = 8, Mensaje = "Registro agregado correctamente." });

            var resultado = await _controller.InsertarAsync(crearDto);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Equal(8, Assert.IsType<Respuesta<int>>(ok.Value).Dato);
        }
    }
}
