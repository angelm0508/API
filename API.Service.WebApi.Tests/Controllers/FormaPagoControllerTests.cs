using API.Application.DTO;
using API.Application.DTO.formaPago;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class FormaPagoControllerTests
    {
        private readonly Mock<IFormaPagoApplication> _app = new();

        private FormaPagoController Crear() => new(_app.Object);

        [Fact]
        public async Task ObtenerTodo_BadRequestYOk()
        {
            var mala = new Respuesta<IEnumerable<FormaPagoDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<FormaPagoDTO>> { Resultado = true, Dato = new[] { new FormaPagoDTO() } };
            _app.SetupSequence(a => a.ObtenerAsync()).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerTodo()).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerTodo()).Result).Value);
        }

        [Fact]
        public async Task ObtenerPorCodigo_NotFound_CuandoDatoEsNulo()
        {
            _app.Setup(a => a.ObtenerPorCodigoAsync("XXX")).ReturnsAsync(new Respuesta<FormaPagoDTO> { Resultado = true, Dato = null! });

            var resultado = await Crear().ObtenerPorCodigo("XXX");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Equal("Código de forma de pago no encontrado.", Assert.IsType<Respuesta<FormaPagoDTO>>(notFound.Value).Mensaje);
        }

        [Fact]
        public async Task Crear_BadRequestYOk()
        {
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            var dto = new FormaPagoCrearDTO { Codigo = "EFE", Nombre = "Efectivo" };
            _app.SetupSequence(a => a.InsertarAsync(dto)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().Crear(dto)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Crear(dto)).Result).Value);
        }

        [Fact]
        public async Task Actualizar_NotFound_CuandoNoExiste()
        {
            _app.Setup(a => a.ObtenerPorCodigoAsync("XXX")).ReturnsAsync(new Respuesta<FormaPagoDTO> { Resultado = true, Dato = null! });

            var resultado = await Crear().Actualizar("XXX", new FormaPagoActualizarDTO { Nombre = "X" });

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
            _app.Verify(a => a.ActualizarAsync(It.IsAny<string>(), It.IsAny<FormaPagoActualizarDTO>()), Times.Never);
        }

        [Fact]
        public async Task Actualizar_Ok_CuandoExiste()
        {
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            var dto = new FormaPagoActualizarDTO { Nombre = "Efectivo caja" };
            _app.Setup(a => a.ObtenerPorCodigoAsync("EFE")).ReturnsAsync(new Respuesta<FormaPagoDTO> { Resultado = true, Dato = new FormaPagoDTO { Codigo = "EFE" } });
            _app.Setup(a => a.ActualizarAsync("EFE", dto)).ReturnsAsync(buena);

            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Actualizar("EFE", dto)).Result).Value);
        }

        [Fact]
        public async Task Eliminar_NotFoundYOk()
        {
            _app.SetupSequence(a => a.ObtenerPorCodigoAsync("EFE"))
                .ReturnsAsync(new Respuesta<FormaPagoDTO> { Resultado = true, Dato = null! })
                .ReturnsAsync(new Respuesta<FormaPagoDTO> { Resultado = true, Dato = new FormaPagoDTO { Codigo = "EFE" } });
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.Setup(a => a.EliminarAsync("EFE")).ReturnsAsync(buena);

            Assert.IsType<NotFoundObjectResult>((await Crear().Eliminar("EFE")).Result);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Eliminar("EFE")).Result).Value);
        }
    }
}
