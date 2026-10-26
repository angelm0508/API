using API.Application.Main;
using API.Domain.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class AntiguedadApplicationTests
    {
        private readonly Mock<IAntiguedadService> _servicio = new();
        private readonly AntiguedadApplication _app;

        public AntiguedadApplicationTests()
        {
            _app = new AntiguedadApplication(_servicio.Object);
        }

        private static IReadOnlyList<FilaAntiguedad> Filas() => new List<FilaAntiguedad>
        {
            new("C001", "Cliente Uno", 100m, 10m, 20m, 30m, 40m, 200m),
            new("C002", "Cliente Dos", 0m, 5m, 0m, 0m, 0m, 5m)
        };

        private static IReadOnlyList<DocumentoAntiguedad> Documentos() => new List<DocumentoAntiguedad>
        {
            new(7, "FAC", 123, new DateTime(2026, 8, 1), new DateTime(2026, 8, 31), 56, 80m),
            new(9, "ND", 4, new DateTime(2026, 10, 1), new DateTime(2026, 11, 1), -6, 20m)
        };

        [Fact]
        public async Task ObtenerCobrar_UsaTipoCYMapeaLasFilas()
        {
            _servicio.Setup(s => s.ObtenerAsync("C")).ReturnsAsync(Filas());

            var respuesta = await _app.ObtenerCobrarAsync();

            Assert.True(respuesta.Resultado);
            Assert.Equal(2, respuesta.Dato.Count);
            var f = respuesta.Dato[0];
            Assert.Equal("C001", f.CodigoSn);
            Assert.Equal("Cliente Uno", f.NombreSn);
            Assert.Equal(100m, f.Vigente);
            Assert.Equal(10m, f.De1a30);
            Assert.Equal(20m, f.De31a60);
            Assert.Equal(30m, f.De61a90);
            Assert.Equal(40m, f.Mas90);
            Assert.Equal(200m, f.Total);
            Assert.Equal("C002", respuesta.Dato[1].CodigoSn);
            _servicio.Verify(s => s.ObtenerAsync("P"), Times.Never);
        }

        [Fact]
        public async Task ObtenerPagar_UsaTipoPYMapeaLasFilas()
        {
            _servicio.Setup(s => s.ObtenerAsync("P")).ReturnsAsync(Filas());

            var respuesta = await _app.ObtenerPagarAsync();

            Assert.True(respuesta.Resultado);
            Assert.Equal(2, respuesta.Dato.Count);
            _servicio.Verify(s => s.ObtenerAsync("P"), Times.Once);
            _servicio.Verify(s => s.ObtenerAsync("C"), Times.Never);
        }

        [Fact]
        public async Task Obtener_SinFilas_DevuelveListaVaciaConResultadoVerdadero()
        {
            _servicio.Setup(s => s.ObtenerAsync("C")).ReturnsAsync(new List<FilaAntiguedad>());

            var respuesta = await _app.ObtenerCobrarAsync();

            Assert.True(respuesta.Resultado);
            Assert.NotNull(respuesta.Dato);
            Assert.Empty(respuesta.Dato);
        }

        [Fact]
        public async Task Obtener_ErrorDelServicio_DevuelveResultadoFalsoConElMensaje()
        {
            _servicio.Setup(s => s.ObtenerAsync("C")).ThrowsAsync(new Exception("El tipo debe ser C o P."));
            _servicio.Setup(s => s.ObtenerAsync("P")).ThrowsAsync(new Exception("fallo"));

            var cobrar = await _app.ObtenerCobrarAsync();
            var pagar = await _app.ObtenerPagarAsync();

            Assert.False(cobrar.Resultado);
            Assert.Equal("El tipo debe ser C o P.", cobrar.Mensaje);
            Assert.False(pagar.Resultado);
            Assert.Equal("fallo", pagar.Mensaje);
        }

        [Fact]
        public async Task DetalleCobrar_UsaTipoCYMapeaLosDocumentos()
        {
            _servicio.Setup(s => s.ObtenerDetalleAsync("C", "C001")).ReturnsAsync(Documentos());

            var respuesta = await _app.ObtenerDetalleCobrarAsync("C001");

            Assert.True(respuesta.Resultado);
            Assert.Equal(2, respuesta.Dato.Count);
            var d = respuesta.Dato[0];
            Assert.Equal(7, d.Entry);
            Assert.Equal("FAC", d.Tipo);
            Assert.Equal(123, d.NumDoc);
            Assert.Equal(new DateTime(2026, 8, 1), d.FechaDoc);
            Assert.Equal(new DateTime(2026, 8, 31), d.Vencimiento);
            Assert.Equal(56, d.DiasVencido);
            Assert.Equal(80m, d.Saldo);
            Assert.Equal(-6, respuesta.Dato[1].DiasVencido);
            _servicio.Verify(s => s.ObtenerDetalleAsync("P", It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DetallePagar_UsaTipoP()
        {
            _servicio.Setup(s => s.ObtenerDetalleAsync("P", "P001")).ReturnsAsync(Documentos());

            var respuesta = await _app.ObtenerDetallePagarAsync("P001");

            Assert.True(respuesta.Resultado);
            Assert.Equal(2, respuesta.Dato.Count);
            _servicio.Verify(s => s.ObtenerDetalleAsync("P", "P001"), Times.Once);
            _servicio.Verify(s => s.ObtenerDetalleAsync("C", It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Detalle_SinDocumentos_DevuelveListaVaciaConResultadoVerdadero()
        {
            _servicio.Setup(s => s.ObtenerDetalleAsync("C", "X")).ReturnsAsync(new List<DocumentoAntiguedad>());

            var respuesta = await _app.ObtenerDetalleCobrarAsync("X");

            Assert.True(respuesta.Resultado);
            Assert.NotNull(respuesta.Dato);
            Assert.Empty(respuesta.Dato);
        }

        [Fact]
        public async Task Detalle_ErrorDelServicio_DevuelveResultadoFalsoConElMensaje()
        {
            _servicio.Setup(s => s.ObtenerDetalleAsync("C", "C001")).ThrowsAsync(new Exception("fallo cobrar"));
            _servicio.Setup(s => s.ObtenerDetalleAsync("P", "P001")).ThrowsAsync(new Exception("fallo pagar"));

            var cobrar = await _app.ObtenerDetalleCobrarAsync("C001");
            var pagar = await _app.ObtenerDetallePagarAsync("P001");

            Assert.False(cobrar.Resultado);
            Assert.Equal("fallo cobrar", cobrar.Mensaje);
            Assert.False(pagar.Resultado);
            Assert.Equal("fallo pagar", pagar.Mensaje);
        }
    }
}
