using API.Application.DTO;
using API.Application.DTO.pagoRecibido;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class PagoRecibidoControllerTests
    {
        private readonly Mock<IPagoRecibidoApplication> _app = new();

        private PagoRecibidoController Crear() => new(_app.Object);

        [Fact]
        public async Task ObtenerTodo_BadRequestYOk()
        {
            var mala = new Respuesta<IEnumerable<PagoRecibidoDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<PagoRecibidoDTO>> { Resultado = true, Dato = new[] { new PagoRecibidoDTO() } };
            _app.SetupSequence(a => a.ObtenerTodoAsync()).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerTodo()).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerTodo()).Result).Value);
        }

        [Fact]
        public async Task Obtener_NotFound_CuandoDatoEsNulo()
        {
            _app.Setup(a => a.ObtenerAsync(9)).ReturnsAsync(new Respuesta<PagoRecibidoDTO> { Resultado = true, Dato = null! });

            var resultado = await Crear().Obtener(9);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Equal("El pago recibido no se encontró.", Assert.IsType<Respuesta<PagoRecibidoDTO>>(notFound.Value).Mensaje);
        }

        [Fact]
        public async Task Obtener_Ok_CuandoExiste()
        {
            var buena = new Respuesta<PagoRecibidoDTO> { Resultado = true, Dato = new PagoRecibidoDTO { Entry = 9 } };
            _app.Setup(a => a.ObtenerAsync(9)).ReturnsAsync(buena);

            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Obtener(9)).Result).Value);
        }

        [Fact]
        public async Task ObtenerFacturasYMedios_BadRequestYOk()
        {
            var malaF = new Respuesta<IEnumerable<PagoRecibidoFacturaDTO>> { Resultado = false, Mensaje = "error" };
            var buenaF = new Respuesta<IEnumerable<PagoRecibidoFacturaDTO>> { Resultado = true, Dato = new[] { new PagoRecibidoFacturaDTO() } };
            _app.SetupSequence(a => a.ObtenerFacturasAsync(9)).ReturnsAsync(malaF).ReturnsAsync(buenaF);
            var malaM = new Respuesta<IEnumerable<PagoRecibidoMedioDTO>> { Resultado = false, Mensaje = "error" };
            var buenaM = new Respuesta<IEnumerable<PagoRecibidoMedioDTO>> { Resultado = true, Dato = new[] { new PagoRecibidoMedioDTO() } };
            _app.SetupSequence(a => a.ObtenerMediosAsync(9)).ReturnsAsync(malaM).ReturnsAsync(buenaM);

            Assert.Same(malaF, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerFacturas(9)).Result).Value);
            Assert.Same(buenaF, Assert.IsType<OkObjectResult>((await Crear().ObtenerFacturas(9)).Result).Value);
            Assert.Same(malaM, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerMedios(9)).Result).Value);
            Assert.Same(buenaM, Assert.IsType<OkObjectResult>((await Crear().ObtenerMedios(9)).Result).Value);
        }

        [Fact]
        public async Task ObtenerDocumentosPorCobrar_BadRequestYOk()
        {
            var mala = new Respuesta<IEnumerable<DocumentoPorCobrarDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<DocumentoPorCobrarDTO>> { Resultado = true, Dato = new[] { new DocumentoPorCobrarDTO() } };
            _app.SetupSequence(a => a.ObtenerDocumentosPorCobrarAsync("SN1")).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerDocumentosPorCobrar("SN1")).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerDocumentosPorCobrar("SN1")).Result).Value);
        }

        [Fact]
        public async Task Crear_BadRequestYOk()
        {
            var mala = new Respuesta<int> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<int> { Resultado = true, Dato = 5 };
            var dto = new PagoRecibidoCrearDTO { Serie = 40, CodigoSn = "SN1" };
            _app.SetupSequence(a => a.InsertarAsync(dto)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().Crear(dto)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Crear(dto)).Result).Value);
        }

        [Fact]
        public async Task Cancelar_NotFound_CuandoNoExiste()
        {
            _app.Setup(a => a.ObtenerAsync(9)).ReturnsAsync(new Respuesta<PagoRecibidoDTO> { Resultado = true, Dato = null! });

            var resultado = await Crear().Cancelar(9);

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
            _app.Verify(a => a.CancelarAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Cancelar_BadRequestYOk()
        {
            _app.Setup(a => a.ObtenerAsync(9)).ReturnsAsync(new Respuesta<PagoRecibidoDTO> { Resultado = true, Dato = new PagoRecibidoDTO { Entry = 9 } });
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "El pago ya está cancelado." };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.SetupSequence(a => a.CancelarAsync(9)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().Cancelar(9)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Cancelar(9)).Result).Value);
        }
    }
}
