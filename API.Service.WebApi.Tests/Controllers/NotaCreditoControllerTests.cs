using System.Security.Claims;
using API.Application.DTO;
using API.Application.DTO.notaCredito;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class NotaCreditoControllerTests
    {
        private readonly Mock<INotaCreditoApplication> _app = new();

        private NotaCreditoController Crear(params Claim[] claims) => new(_app.Object)
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
            var respuesta = new Respuesta<NotaCreditoDTO> { Resultado = false, Mensaje = "error" };
            _app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await Crear().Obtener(1);

            Assert.Same(respuesta, Assert.IsType<BadRequestObjectResult>(resultado.Result).Value);
        }

        [Fact]
        public async Task Obtener_NotFound_CuandoDatoEsNulo()
        {
            _app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(new Respuesta<NotaCreditoDTO> { Resultado = true, Dato = null! });

            var resultado = await Crear().Obtener(1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<NotaCreditoDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("El código de la nota de crédito no se encontró.", valor.Mensaje);
        }

        [Fact]
        public async Task Obtener_Ok_CuandoExiste()
        {
            var respuesta = new Respuesta<NotaCreditoDTO> { Resultado = true, Dato = new NotaCreditoDTO { Entry = 1 } };
            _app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await Crear().Obtener(1);

            Assert.Same(respuesta, Assert.IsType<OkObjectResult>(resultado.Result).Value);
        }

        [Fact]
        public async Task ObtenerTodo_BadRequestYOk()
        {
            var mala = new Respuesta<IEnumerable<NotaCreditoDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<NotaCreditoDTO>> { Resultado = true, Dato = new[] { new NotaCreditoDTO() } };
            _app.SetupSequence(a => a.ObtenerTodoAsync()).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerTodoAsync()).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerTodoAsync()).Result).Value);
        }

        [Fact]
        public async Task ObtenerLineas_BadRequestYOk()
        {
            var mala = new Respuesta<IEnumerable<NotaCreditoDetalleDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<NotaCreditoDetalleDTO>> { Resultado = true, Dato = new[] { new NotaCreditoDetalleDTO() } };
            _app.SetupSequence(a => a.ObtenerLineasAsync(5)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerLineas(5)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerLineas(5)).Result).Value);
        }

        [Fact]
        public async Task ObtenerFacturasBase_BadRequestYOk()
        {
            var mala = new Respuesta<IEnumerable<NotaCreditoFacturaBaseResumenDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<NotaCreditoFacturaBaseResumenDTO>> { Resultado = true, Dato = new[] { new NotaCreditoFacturaBaseResumenDTO() } };
            _app.SetupSequence(a => a.ObtenerFacturasBaseAsync("SN1")).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerFacturasBase("SN1")).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerFacturasBase("SN1")).Result).Value);
        }

        [Fact]
        public async Task ObtenerFacturaBase_BadRequestYOk()
        {
            var mala = new Respuesta<NotaCreditoFacturaBaseDTO> { Resultado = false, Mensaje = "La factura no existe." };
            var buena = new Respuesta<NotaCreditoFacturaBaseDTO> { Resultado = true, Dato = new NotaCreditoFacturaBaseDTO { Entry = 12 } };
            _app.SetupSequence(a => a.ObtenerFacturaBaseAsync(12)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerFacturaBase(12)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerFacturaBase(12)).Result).Value);
        }

        [Fact]
        public async Task Insertar_BadRequestYOk()
        {
            var mala = new Respuesta<int> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<int> { Resultado = true, Dato = 7 };
            _app.SetupSequence(a => a.InsertarAsync(It.IsAny<NotaCreditoCrearDTO>())).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().InsertarAsync(new NotaCreditoCrearDTO())).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().InsertarAsync(new NotaCreditoCrearDTO())).Result).Value);
        }

        private void NotaExistente(int id = 1) =>
            _app.Setup(a => a.ObtenerAsync(id)).ReturnsAsync(new Respuesta<NotaCreditoDTO> { Resultado = true, Dato = new NotaCreditoDTO { Entry = id } });

        [Fact]
        public async Task Actualizar_NotFound_CuandoNoExiste()
        {
            _app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(new Respuesta<NotaCreditoDTO> { Resultado = true, Dato = null! });

            var resultado = await Crear().ActualizarAsync(1, new NotaCreditoActualizarDTO());

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Actualizar_BadRequest_CuandoLaAplicacionFalla()
        {
            NotaExistente();
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _app.Setup(a => a.ActualizarAsync(1, It.IsAny<NotaCreditoActualizarDTO>())).ReturnsAsync(mala);

            var resultado = await Crear(new Claim("SuperUsuario", "N"), new Claim("perm", "NotasCredito:VAU")).ActualizarAsync(1, new NotaCreditoActualizarDTO { Comentario = "x" });

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>(resultado.Result).Value);
        }

        [Fact]
        public async Task Actualizar_CancelarSinEliminar_Devuelve403YNoActualiza()
        {
            NotaExistente();

            var resultado = await Crear(new Claim("SuperUsuario", "N"), new Claim("perm", "NotasCredito:VAU")).ActualizarAsync(1, new NotaCreditoActualizarDTO { Cancelado = "S" });

            AssertForbidden(resultado.Result as ObjectResult);
            _app.Verify(a => a.ActualizarAsync(It.IsAny<int>(), It.IsAny<NotaCreditoActualizarDTO>()), Times.Never);
        }

        [Fact]
        public async Task Actualizar_CancelarConEliminar_Actualiza()
        {
            NotaExistente();
            _app.Setup(a => a.ActualizarAsync(1, It.IsAny<NotaCreditoActualizarDTO>())).ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await Crear(new Claim("SuperUsuario", "N"), new Claim("perm", "NotasCredito:VAUE")).ActualizarAsync(1, new NotaCreditoActualizarDTO { Cancelado = "S" });

            Assert.IsType<OkObjectResult>(resultado.Result);
            _app.Verify(a => a.ActualizarAsync(1, It.IsAny<NotaCreditoActualizarDTO>()), Times.Once);
        }

        [Fact]
        public async Task Actualizar_CancelarComoSuperUsuario_Actualiza()
        {
            NotaExistente();
            _app.Setup(a => a.ActualizarAsync(1, It.IsAny<NotaCreditoActualizarDTO>())).ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await Crear(new Claim("SuperUsuario", "S")).ActualizarAsync(1, new NotaCreditoActualizarDTO { Cancelado = "S" });

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Actualizar_SinCancelar_NoExigeEliminar()
        {
            NotaExistente();
            _app.Setup(a => a.ActualizarAsync(1, It.IsAny<NotaCreditoActualizarDTO>())).ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await Crear(new Claim("SuperUsuario", "N"), new Claim("perm", "NotasCredito:VAU")).ActualizarAsync(1, new NotaCreditoActualizarDTO { Cancelado = "N", Comentario = "x" });

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Eliminar_NotFound_BadRequest_Ok()
        {
            _app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(new Respuesta<NotaCreditoDTO> { Resultado = true, Dato = null! });
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
