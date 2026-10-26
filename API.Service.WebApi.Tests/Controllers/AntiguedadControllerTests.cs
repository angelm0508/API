using API.Application.DTO;
using API.Application.DTO.antiguedad;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class AntiguedadControllerTests
    {
        private readonly Mock<IAntiguedadApplication> _app = new();

        private AntiguedadController Crear() => new(_app.Object);

        private static Respuesta<List<FilaAntiguedadDTO>> Filas(bool ok, string mensaje = "") => new()
        {
            Resultado = ok,
            Mensaje = mensaje,
            Dato = ok ? new List<FilaAntiguedadDTO> { new() { CodigoSn = "C001" } } : null!
        };

        private static Respuesta<List<DocumentoAntiguedadDTO>> Docs(bool ok, string mensaje = "") => new()
        {
            Resultado = ok,
            Mensaje = mensaje,
            Dato = ok ? new List<DocumentoAntiguedadDTO> { new() { Entry = 1 } } : null!
        };

        [Fact]
        public async Task ObtenerCobrar_Ok_DevuelveLaRespuesta()
        {
            var buena = Filas(true);
            _app.Setup(a => a.ObtenerCobrarAsync()).ReturnsAsync(buena);

            var resultado = await Crear().ObtenerCobrar();

            Assert.Same(buena, Assert.IsType<OkObjectResult>(resultado.Result).Value);
            _app.Verify(a => a.ObtenerPagarAsync(), Times.Never);
        }

        [Fact]
        public async Task ObtenerCobrar_Error_DevuelveBadRequestConElMensaje()
        {
            var mala = Filas(false, "fallo");
            _app.Setup(a => a.ObtenerCobrarAsync()).ReturnsAsync(mala);

            var resultado = await Crear().ObtenerCobrar();

            var bad = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(mala, bad.Value);
            Assert.Equal("fallo", mala.Mensaje);
        }

        [Fact]
        public async Task ObtenerPagar_OkYBadRequest()
        {
            var buena = Filas(true);
            var mala = Filas(false, "fallo");
            _app.SetupSequence(a => a.ObtenerPagarAsync()).ReturnsAsync(buena).ReturnsAsync(mala);

            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerPagar()).Result).Value);
            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerPagar()).Result).Value);
            _app.Verify(a => a.ObtenerCobrarAsync(), Times.Never);
        }

        [Fact]
        public async Task DetalleCobrar_Ok_PasaElCodigo()
        {
            var buena = Docs(true);
            _app.Setup(a => a.ObtenerDetalleCobrarAsync("C001")).ReturnsAsync(buena);

            var resultado = await Crear().DetalleCobrar("C001");

            Assert.Same(buena, Assert.IsType<OkObjectResult>(resultado.Result).Value);
        }

        [Fact]
        public async Task DetalleCobrar_ListaVacia_EsOkNoNotFound()
        {
            var vacia = new Respuesta<List<DocumentoAntiguedadDTO>> { Resultado = true, Dato = new List<DocumentoAntiguedadDTO>() };
            _app.Setup(a => a.ObtenerDetalleCobrarAsync("X")).ReturnsAsync(vacia);

            var resultado = await Crear().DetalleCobrar("X");

            Assert.Same(vacia, Assert.IsType<OkObjectResult>(resultado.Result).Value);
        }

        [Fact]
        public async Task DetalleCobrar_Error_DevuelveBadRequest()
        {
            var mala = Docs(false, "fallo");
            _app.Setup(a => a.ObtenerDetalleCobrarAsync("C001")).ReturnsAsync(mala);

            var resultado = await Crear().DetalleCobrar("C001");

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>(resultado.Result).Value);
        }

        [Fact]
        public async Task DetallePagar_OkYBadRequest()
        {
            var buena = Docs(true);
            var mala = Docs(false, "fallo");
            _app.SetupSequence(a => a.ObtenerDetallePagarAsync("P001")).ReturnsAsync(buena).ReturnsAsync(mala);

            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().DetallePagar("P001")).Result).Value);
            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().DetallePagar("P001")).Result).Value);
            _app.Verify(a => a.ObtenerDetalleCobrarAsync(It.IsAny<string>()), Times.Never);
        }
    }
}
