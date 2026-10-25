using API.Application.DTO;
using API.Application.DTO.pagoEfectuado;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class PagoEfectuadoControllerTests
    {
        private readonly Mock<IPagoEfectuadoApplication> _app = new();

        private PagoEfectuadoController Crear() => new(_app.Object);

        [Fact]
        public async Task ObtenerTodos_BadRequestYOk()
        {
            var mala = new Respuesta<IEnumerable<PagoEfectuadoDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<PagoEfectuadoDTO>> { Resultado = true, Dato = new[] { new PagoEfectuadoDTO() } };
            _app.SetupSequence(a => a.ObtenerTodosAsync()).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerTodos()).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerTodos()).Result).Value);
        }

        [Fact]
        public async Task Obtener_NotFound_CuandoDatoEsNulo()
        {
            _app.Setup(a => a.ObtenerAsync(9)).ReturnsAsync(new Respuesta<PagoEfectuadoDTO> { Resultado = true, Dato = null! });

            var resultado = await Crear().Obtener(9);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Equal("El pago efectuado no se encontró.", Assert.IsType<Respuesta<PagoEfectuadoDTO>>(notFound.Value).Mensaje);
        }

        [Fact]
        public async Task Obtener_Ok_CuandoExiste()
        {
            var buena = new Respuesta<PagoEfectuadoDTO> { Resultado = true, Dato = new PagoEfectuadoDTO { Entry = 9 } };
            _app.Setup(a => a.ObtenerAsync(9)).ReturnsAsync(buena);

            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Obtener(9)).Result).Value);
        }

        [Fact]
        public async Task ObtenerFacturasYMedios_BadRequestYOk()
        {
            var malaF = new Respuesta<IEnumerable<PagoEfectuadoFacturaDTO>> { Resultado = false, Mensaje = "error" };
            var buenaF = new Respuesta<IEnumerable<PagoEfectuadoFacturaDTO>> { Resultado = true, Dato = new[] { new PagoEfectuadoFacturaDTO() } };
            _app.SetupSequence(a => a.ObtenerFacturasAsync(9)).ReturnsAsync(malaF).ReturnsAsync(buenaF);
            var malaM = new Respuesta<IEnumerable<PagoEfectuadoMedioDTO>> { Resultado = false, Mensaje = "error" };
            var buenaM = new Respuesta<IEnumerable<PagoEfectuadoMedioDTO>> { Resultado = true, Dato = new[] { new PagoEfectuadoMedioDTO() } };
            _app.SetupSequence(a => a.ObtenerMediosAsync(9)).ReturnsAsync(malaM).ReturnsAsync(buenaM);

            Assert.Same(malaF, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerFacturas(9)).Result).Value);
            Assert.Same(buenaF, Assert.IsType<OkObjectResult>((await Crear().ObtenerFacturas(9)).Result).Value);
            Assert.Same(malaM, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerMedios(9)).Result).Value);
            Assert.Same(buenaM, Assert.IsType<OkObjectResult>((await Crear().ObtenerMedios(9)).Result).Value);
        }

        [Fact]
        public async Task ObtenerAbiertos_BadRequestYOk()
        {
            var mala = new Respuesta<IEnumerable<DocumentoPorPagarDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<DocumentoPorPagarDTO>> { Resultado = true, Dato = new[] { new DocumentoPorPagarDTO() } };
            _app.SetupSequence(a => a.ObtenerAbiertosAsync("SN1")).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerAbiertos("SN1")).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerAbiertos("SN1")).Result).Value);
        }

        [Fact]
        public async Task Crear_BadRequestYOk()
        {
            var mala = new Respuesta<int> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<int> { Resultado = true, Dato = 5 };
            var dto = new PagoEfectuadoCrearDTO { Serie = 40, CodigoSn = "SN1" };
            _app.SetupSequence(a => a.InsertarAsync(dto)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().Crear(dto)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Crear(dto)).Result).Value);
        }

        [Fact]
        public async Task Cancelar_NotFound_CuandoNoExiste()
        {
            _app.Setup(a => a.ObtenerAsync(9)).ReturnsAsync(new Respuesta<PagoEfectuadoDTO> { Resultado = true, Dato = null! });

            var resultado = await Crear().Cancelar(9);

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
            _app.Verify(a => a.CancelarAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Cancelar_BadRequestYOk()
        {
            _app.Setup(a => a.ObtenerAsync(9)).ReturnsAsync(new Respuesta<PagoEfectuadoDTO> { Resultado = true, Dato = new PagoEfectuadoDTO { Entry = 9 } });
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "El pago ya está cancelado." };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.SetupSequence(a => a.CancelarAsync(9)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().Cancelar(9)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Cancelar(9)).Result).Value);
        }
    }
}
