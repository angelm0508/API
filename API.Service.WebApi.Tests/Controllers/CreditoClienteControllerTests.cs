using API.Application.DTO;
using API.Application.DTO.creditoCliente;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class CreditoClienteControllerTests
    {
        private readonly Mock<ICreditoClienteApplication> _app = new();

        private CreditoClienteController Crear() => new(_app.Object);

        [Fact]
        public async Task Obtener_Ok_DevuelveLaPosicion()
        {
            var buena = new Respuesta<PosicionCreditoDTO> { Resultado = true, Dato = new PosicionCreditoDTO { CodigoSn = "C001" } };
            _app.Setup(a => a.ObtenerPosicionAsync("C001")).ReturnsAsync(buena);

            var resultado = await Crear().Obtener("C001");

            Assert.Same(buena, Assert.IsType<OkObjectResult>(resultado.Result).Value);
        }

        [Fact]
        public async Task Obtener_ClienteInexistente_DevuelveNotFoundConElMensaje()
        {
            var inexistente = new Respuesta<PosicionCreditoDTO> { Resultado = true, Dato = null!, Mensaje = "El cliente no existe." };
            _app.Setup(a => a.ObtenerPosicionAsync("X")).ReturnsAsync(inexistente);

            var resultado = await Crear().Obtener("X");

            var noEncontrado = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var cuerpo = Assert.IsType<Respuesta<PosicionCreditoDTO>>(noEncontrado.Value);
            Assert.False(cuerpo.Resultado);
            Assert.Equal("El cliente no existe.", cuerpo.Mensaje);
        }

        [Fact]
        public async Task Obtener_ErrorDeNegocio_DevuelveBadRequest()
        {
            var mala = new Respuesta<PosicionCreditoDTO> { Resultado = false, Mensaje = "falla" };
            _app.Setup(a => a.ObtenerPosicionAsync("C001")).ReturnsAsync(mala);

            var resultado = await Crear().Obtener("C001");

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>(resultado.Result).Value);
        }

        [Fact]
        public async Task Posiciones_BadRequestYOk()
        {
            var codigos = new List<string> { "C001", "C002" };
            var mala = new Respuesta<IEnumerable<PosicionCreditoDTO>> { Resultado = false, Mensaje = "Demasiados socios en la consulta." };
            var buena = new Respuesta<IEnumerable<PosicionCreditoDTO>> { Resultado = true, Dato = new[] { new PosicionCreditoDTO { CodigoSn = "C001" } } };
            _app.SetupSequence(a => a.ObtenerPosicionesAsync(codigos)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().Posiciones(codigos)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Posiciones(codigos)).Result).Value);
        }

        [Fact]
        public async Task Posiciones_CuerpoNulo_SeTrataComoListaVacia()
        {
            var vacia = new Respuesta<IEnumerable<PosicionCreditoDTO>> { Resultado = true, Dato = Array.Empty<PosicionCreditoDTO>() };
            _app.Setup(a => a.ObtenerPosicionesAsync(It.Is<IEnumerable<string>>(c => !c.Any()))).ReturnsAsync(vacia);

            Assert.Same(vacia, Assert.IsType<OkObjectResult>((await Crear().Posiciones(null)).Result).Value);
        }
    }
}
