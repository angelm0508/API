using System.Security.Claims;
using API.Application.DTO;
using API.Application.DTO.notaDebito;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class NotaDebitoControllerTests
    {
        private readonly Mock<INotaDebitoApplication> _app = new();

        private NotaDebitoController Crear(params Claim[] claims) => new(_app.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
            }
        };

        private static void AssertForbidden(ObjectResult? resultado)
        {
            Assert.NotNull(resultado);
            Assert.Equal(StatusCodes.Status403Forbidden, resultado!.StatusCode);
            var cuerpo = Assert.IsType<Respuesta<object>>(resultado.Value);
            Assert.Equal("No tenés permiso para realizar esta acción.", cuerpo.Mensaje);
        }

        [Fact]
        public async Task Obtener_BadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<NotaDebitoDTO> { Resultado = false, Mensaje = "error" };
            _app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await Crear().Obtener(1);

            Assert.Same(respuesta, Assert.IsType<BadRequestObjectResult>(resultado.Result).Value);
        }

        [Fact]
        public async Task Obtener_NotFound_CuandoDatoEsNulo()
        {
            _app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(new Respuesta<NotaDebitoDTO> { Resultado = true, Dato = null! });

            var resultado = await Crear().Obtener(1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<NotaDebitoDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("El código de la nota de débito no se encontró.", valor.Mensaje);
        }

        [Fact]
        public async Task Obtener_Ok_CuandoExiste()
        {
            var respuesta = new Respuesta<NotaDebitoDTO> { Resultado = true, Dato = new NotaDebitoDTO { Entry = 1 } };
            _app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await Crear().Obtener(1);

            Assert.Same(respuesta, Assert.IsType<OkObjectResult>(resultado.Result).Value);
        }

        [Fact]
        public async Task ObtenerTodo_BadRequestYOk()
        {
            var mala = new Respuesta<IEnumerable<NotaDebitoDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<NotaDebitoDTO>> { Resultado = true, Dato = new[] { new NotaDebitoDTO() } };
            _app.SetupSequence(a => a.ObtenerTodoAsync()).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerTodoAsync()).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerTodoAsync()).Result).Value);
        }

        [Fact]
        public async Task ObtenerLineas_BadRequestYOk()
        {
            var mala = new Respuesta<IEnumerable<NotaDebitoDetalleDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<NotaDebitoDetalleDTO>> { Resultado = true, Dato = new[] { new NotaDebitoDetalleDTO() } };
            _app.SetupSequence(a => a.ObtenerLineasAsync(5)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerLineas(5)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerLineas(5)).Result).Value);
        }

        [Fact]
        public async Task ObtenerFacturasBase_BadRequestYOk()
        {
            var mala = new Respuesta<IEnumerable<NotaDebitoFacturaBaseResumenDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<NotaDebitoFacturaBaseResumenDTO>> { Resultado = true, Dato = new[] { new NotaDebitoFacturaBaseResumenDTO() } };
            _app.SetupSequence(a => a.ObtenerFacturasBaseAsync("SN1")).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerFacturasBase("SN1")).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerFacturasBase("SN1")).Result).Value);
        }

        [Fact]
        public async Task ObtenerFacturaBase_BadRequestYOk()
        {
            var mala = new Respuesta<NotaDebitoFacturaBaseDTO> { Resultado = false, Mensaje = "La factura no existe." };
            var buena = new Respuesta<NotaDebitoFacturaBaseDTO> { Resultado = true, Dato = new NotaDebitoFacturaBaseDTO { Entry = 12 } };
            _app.SetupSequence(a => a.ObtenerFacturaBaseAsync(12)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerFacturaBase(12)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerFacturaBase(12)).Result).Value);
        }

        [Fact]
        public async Task Insertar_BadRequestYOk()
        {
            var mala = new Respuesta<int> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<int> { Resultado = true, Dato = 7 };
            _app.SetupSequence(a => a.InsertarAsync(It.IsAny<NotaDebitoCrearDTO>())).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().InsertarAsync(new NotaDebitoCrearDTO())).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().InsertarAsync(new NotaDebitoCrearDTO())).Result).Value);
        }

        private void NotaExistente(int id = 1) =>
            _app.Setup(a => a.ObtenerAsync(id)).ReturnsAsync(new Respuesta<NotaDebitoDTO> { Resultado = true, Dato = new NotaDebitoDTO { Entry = id } });

        [Fact]
        public async Task Actualizar_NotFound_CuandoNoExiste()
        {
            _app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(new Respuesta<NotaDebitoDTO> { Resultado = true, Dato = null! });

            var resultado = await Crear().ActualizarAsync(1, new NotaDebitoActualizarDTO());

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Actualizar_BadRequest_CuandoLaAplicacionFalla()
        {
            NotaExistente();
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _app.Setup(a => a.ActualizarAsync(1, It.IsAny<NotaDebitoActualizarDTO>())).ReturnsAsync(mala);

            var resultado = await Crear(new Claim("SuperUsuario", "N"), new Claim("perm", "NotasDebito:VAU")).ActualizarAsync(1, new NotaDebitoActualizarDTO { Comentario = "x" });

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>(resultado.Result).Value);
        }

        [Fact]
        public async Task Actualizar_CancelarSinEliminar_Devuelve403YNoActualiza()
        {
            NotaExistente();

            var resultado = await Crear(new Claim("SuperUsuario", "N"), new Claim("perm", "NotasDebito:VAU")).ActualizarAsync(1, new NotaDebitoActualizarDTO { Cancelado = "S" });

            AssertForbidden(resultado.Result as ObjectResult);
            _app.Verify(a => a.ActualizarAsync(It.IsAny<int>(), It.IsAny<NotaDebitoActualizarDTO>()), Times.Never);
        }

        [Fact]
        public async Task Actualizar_CancelarConEliminar_Actualiza()
        {
            NotaExistente();
            _app.Setup(a => a.ActualizarAsync(1, It.IsAny<NotaDebitoActualizarDTO>())).ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await Crear(new Claim("SuperUsuario", "N"), new Claim("perm", "NotasDebito:VAUE")).ActualizarAsync(1, new NotaDebitoActualizarDTO { Cancelado = "S" });

            Assert.IsType<OkObjectResult>(resultado.Result);
            _app.Verify(a => a.ActualizarAsync(1, It.IsAny<NotaDebitoActualizarDTO>()), Times.Once);
        }

        [Fact]
        public async Task Actualizar_CancelarComoSuperUsuario_Actualiza()
        {
            NotaExistente();
            _app.Setup(a => a.ActualizarAsync(1, It.IsAny<NotaDebitoActualizarDTO>())).ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await Crear(new Claim("SuperUsuario", "S")).ActualizarAsync(1, new NotaDebitoActualizarDTO { Cancelado = "S" });

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Actualizar_SinCancelar_NoExigeEliminar()
        {
            NotaExistente();
            _app.Setup(a => a.ActualizarAsync(1, It.IsAny<NotaDebitoActualizarDTO>())).ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await Crear(new Claim("SuperUsuario", "N"), new Claim("perm", "NotasDebito:VAU")).ActualizarAsync(1, new NotaDebitoActualizarDTO { Cancelado = "N", Comentario = "x" });

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Eliminar_NotFound_BadRequest_Ok()
        {
            _app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(new Respuesta<NotaDebitoDTO> { Resultado = true, Dato = null! });
            Assert.IsType<NotFoundObjectResult>((await Crear().EliminarAsync(1)).Result);

            NotaExistente(2);
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "Cancele el documento (Cancelado='S') antes de eliminarlo." };
            _app.Setup(a => a.EliminarAsync(2)).ReturnsAsync(mala);
            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().EliminarAsync(2)).Result).Value);

            NotaExistente(3);
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.Setup(a => a.EliminarAsync(3)).ReturnsAsync(buena);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().EliminarAsync(3)).Result).Value);
        }
    }
}
