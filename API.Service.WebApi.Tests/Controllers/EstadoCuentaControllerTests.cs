using API.Application.DTO;
using API.Application.DTO.estadoCuenta;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class EstadoCuentaControllerTests
    {
        private readonly Mock<IEstadoCuentaApplication> _app = new();

        private EstadoCuentaController Crear() => new(_app.Object);

        [Fact]
        public async Task Obtener_Ok_PasaSocioYFechas()
        {
            var desde = new DateTime(2026, 1, 1);
            var hasta = new DateTime(2026, 6, 30);
            var buena = new Respuesta<EstadoCuentaDTO> { Resultado = true, Dato = new EstadoCuentaDTO { CodigoSn = "C001" } };
            _app.Setup(a => a.ObtenerAsync("C001", desde, hasta)).ReturnsAsync(buena);

            var resultado = await Crear().Obtener("C001", desde, hasta);

            Assert.Same(buena, Assert.IsType<OkObjectResult>(resultado.Result).Value);
        }

        [Fact]
        public async Task Obtener_SinFechas_PasaNulos()
        {
            var buena = new Respuesta<EstadoCuentaDTO> { Resultado = true, Dato = new EstadoCuentaDTO { CodigoSn = "C001" } };
            _app.Setup(a => a.ObtenerAsync("C001", null, null)).ReturnsAsync(buena);

            var resultado = await Crear().Obtener("C001", null, null);

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Obtener_ErrorDeNegocio_DevuelveBadRequestConElMensaje()
        {
            var mala = new Respuesta<EstadoCuentaDTO> { Resultado = false, Mensaje = "La fecha inicial no puede ser posterior a la final." };
            _app.Setup(a => a.ObtenerAsync("C001", It.IsAny<DateTime?>(), It.IsAny<DateTime?>())).ReturnsAsync(mala);

            var resultado = await Crear().Obtener("C001", new DateTime(2026, 5, 1), new DateTime(2026, 4, 1));

            var bad = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(mala, bad.Value);
            Assert.Equal("La fecha inicial no puede ser posterior a la final.", mala.Mensaje);
        }

        [Fact]
        public async Task Obtener_SocioInexistente_DevuelveNotFound()
        {
            var inexistente = new Respuesta<EstadoCuentaDTO> { Resultado = true, Dato = null!, Mensaje = "El socio de negocio no existe." };
            _app.Setup(a => a.ObtenerAsync("X", null, null)).ReturnsAsync(inexistente);

            var resultado = await Crear().Obtener("X", null, null);

            var noEncontrado = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var cuerpo = Assert.IsType<Respuesta<EstadoCuentaDTO>>(noEncontrado.Value);
            Assert.False(cuerpo.Resultado);
            Assert.Equal("El socio de negocio no existe.", cuerpo.Mensaje);
        }

        [Fact]
        public async Task Saldos_BadRequestYOk()
        {
            var codigos = new List<string> { "C001", "P003" };
            var mala = new Respuesta<IEnumerable<SaldoSocioDTO>> { Resultado = false, Mensaje = "Demasiados socios en la consulta." };
            var buena = new Respuesta<IEnumerable<SaldoSocioDTO>> { Resultado = true, Dato = new[] { new SaldoSocioDTO { CodigoSn = "C001" } } };
            _app.SetupSequence(a => a.ObtenerSaldosAsync(codigos)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().Saldos(codigos)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Saldos(codigos)).Result).Value);
        }

        [Fact]
        public async Task Saldos_CuerpoNulo_SeTrataComoListaVacia()
        {
            var vacia = new Respuesta<IEnumerable<SaldoSocioDTO>> { Resultado = true, Dato = Array.Empty<SaldoSocioDTO>() };
            _app.Setup(a => a.ObtenerSaldosAsync(It.Is<IEnumerable<string>>(c => !c.Any()))).ReturnsAsync(vacia);

            Assert.Same(vacia, Assert.IsType<OkObjectResult>((await Crear().Saldos(null)).Result).Value);
        }
    }
}
