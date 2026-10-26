using API.Application.DTO;
using API.Application.DTO.condicionPago;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class CondicionPagoControllerTests
    {
        private readonly Mock<ICondicionPagoApplication> _app = new();

        private CondicionPagoController Crear() => new(_app.Object);

        [Fact]
        public async Task ObtenerTodo_BadRequestYOk()
        {
            var mala = new Respuesta<IEnumerable<CondicionPagoDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<CondicionPagoDTO>> { Resultado = true, Dato = new[] { new CondicionPagoDTO() } };
            _app.SetupSequence(a => a.ObtenerAsync()).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerTodo()).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerTodo()).Result).Value);
        }

        [Fact]
        public async Task ObtenerPorCodigo_NotFound_CuandoDatoEsNulo()
        {
            _app.Setup(a => a.ObtenerPorCodigoAsync("XXX")).ReturnsAsync(new Respuesta<CondicionPagoDTO> { Resultado = true, Dato = null! });

            var resultado = await Crear().ObtenerPorCodigo("XXX");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Equal("Código de condición de pago no encontrado.", Assert.IsType<Respuesta<CondicionPagoDTO>>(notFound.Value).Mensaje);
        }

        [Fact]
        public async Task ObtenerPorCodigo_BadRequest_Y_Ok()
        {
            var mala = new Respuesta<CondicionPagoDTO> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<CondicionPagoDTO> { Resultado = true, Dato = new CondicionPagoDTO { Codigo = "C30" } };
            _app.SetupSequence(a => a.ObtenerPorCodigoAsync("C30")).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerPorCodigo("C30")).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerPorCodigo("C30")).Result).Value);
        }

        [Fact]
        public async Task Crear_BadRequestYOk()
        {
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            var dto = new CondicionPagoCrearDTO { Codigo = "C20", Nombre = "Crédito 20 días", Dias = 20 };
            _app.SetupSequence(a => a.InsertarAsync(dto)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().Crear(dto)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Crear(dto)).Result).Value);
        }

        [Fact]
        public async Task Actualizar_NotFound_CuandoNoExiste()
        {
            _app.Setup(a => a.ObtenerPorCodigoAsync("XXX")).ReturnsAsync(new Respuesta<CondicionPagoDTO> { Resultado = true, Dato = null! });

            var resultado = await Crear().Actualizar("XXX", new CondicionPagoActualizarDTO { Nombre = "X", Dias = 1 });

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
            _app.Verify(a => a.ActualizarAsync(It.IsAny<string>(), It.IsAny<CondicionPagoActualizarDTO>()), Times.Never);
        }

        [Fact]
        public async Task Actualizar_BadRequestYOk_CuandoExiste()
        {
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            var dto = new CondicionPagoActualizarDTO { Nombre = "Crédito 30 días", Dias = 30 };
            _app.Setup(a => a.ObtenerPorCodigoAsync("C30")).ReturnsAsync(new Respuesta<CondicionPagoDTO> { Resultado = true, Dato = new CondicionPagoDTO { Codigo = "C30" } });
            _app.SetupSequence(a => a.ActualizarAsync("C30", dto)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().Actualizar("C30", dto)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Actualizar("C30", dto)).Result).Value);
        }

        [Fact]
        public async Task Eliminar_NotFound_BadRequestYOk()
        {
            _app.SetupSequence(a => a.ObtenerPorCodigoAsync("C30"))
                .ReturnsAsync(new Respuesta<CondicionPagoDTO> { Resultado = true, Dato = null! })
                .ReturnsAsync(new Respuesta<CondicionPagoDTO> { Resultado = true, Dato = new CondicionPagoDTO { Codigo = "C30" } })
                .ReturnsAsync(new Respuesta<CondicionPagoDTO> { Resultado = true, Dato = new CondicionPagoDTO { Codigo = "C30" } });
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.SetupSequence(a => a.EliminarAsync("C30")).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.IsType<NotFoundObjectResult>((await Crear().Eliminar("C30")).Result);
            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().Eliminar("C30")).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Eliminar("C30")).Result).Value);
        }
    }
}
