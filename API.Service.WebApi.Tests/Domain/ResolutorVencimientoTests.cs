using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class ResolutorVencimientoTests
    {
        private readonly Mock<ICondicionPagoDomain> _condiciones = new();
        private readonly ResolutorVencimiento _resolutor;

        public ResolutorVencimientoTests()
        {
            _resolutor = new ResolutorVencimiento(_condiciones.Object);
            Activa("CON", 0);
            Activa("C15", 15);
            Activa("C30", 30);
        }

        private void Activa(string codigo, int dias) =>
            _condiciones.Setup(c => c.ObtenerActivaAsync(codigo))
                .ReturnsAsync(new CondicionPago { Codigo = codigo, Nombre = codigo, Dias = dias, Activo = "S" });

        [Fact]
        public async Task CondicionPedida_GanaAlSocioYAContado()
        {
            var (condicion, _) = await _resolutor.ResolverAsync("C15", "C30", new DateTime(2026, 1, 10));

            Assert.Equal("C15", condicion);
            _condiciones.Verify(c => c.ObtenerActivaAsync("C30"), Times.Never);
        }

        [Fact]
        public async Task SinPedida_UsaLaDelSocio()
        {
            var (condicion, _) = await _resolutor.ResolverAsync(null, "C30", new DateTime(2026, 1, 10));

            Assert.Equal("C30", condicion);
        }

        [Fact]
        public async Task SinPedidaNiDelSocio_UsaContado()
        {
            var (condicion, vencimiento) = await _resolutor.ResolverAsync(null, null, new DateTime(2026, 1, 10));

            Assert.Equal("CON", condicion);
            Assert.Equal(new DateTime(2026, 1, 10), vencimiento);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task PedidaVaciaOEnBlanco_SeTrataComoAusente(string pedida)
        {
            var (condicion, _) = await _resolutor.ResolverAsync(pedida, "C30", new DateTime(2026, 1, 10));

            Assert.Equal("C30", condicion);
        }

        [Fact]
        public async Task TextoConEspacios_SeRecorta()
        {
            var (condicion, vencimiento) = await _resolutor.ResolverAsync("  C15  ", null, new DateTime(2026, 1, 10));

            Assert.Equal("C15", condicion);
            Assert.Equal(new DateTime(2026, 1, 25), vencimiento);
            _condiciones.Verify(c => c.ObtenerActivaAsync("C15"), Times.Once);
        }

        [Fact]
        public async Task Vencimiento_EsFechaDocMasDias()
        {
            var (_, vencimiento) = await _resolutor.ResolverAsync("C30", null, new DateTime(2026, 1, 31));

            Assert.Equal(new DateTime(2026, 3, 2), vencimiento);   // 2026 no es bisiesto: 31/01 + 30 = 02/03
        }

        [Fact]
        public async Task CeroDias_VenceElMismoDia()
        {
            var (_, vencimiento) = await _resolutor.ResolverAsync("CON", null, new DateTime(2026, 5, 20));

            Assert.Equal(new DateTime(2026, 5, 20), vencimiento);
        }

        [Fact]
        public async Task FechaDocConHora_SeCalculaSobreLaFecha()
        {
            var (_, vencimiento) = await _resolutor.ResolverAsync("C15", null, new DateTime(2026, 5, 20, 17, 45, 0));

            Assert.Equal(new DateTime(2026, 6, 4), vencimiento);
            Assert.Equal(TimeSpan.Zero, vencimiento.TimeOfDay);
        }

        [Fact]
        public async Task FechaDocNula_UsaLaFechaDeHoy()
        {
            var (_, vencimiento) = await _resolutor.ResolverAsync("C15", null, null);

            var esperado = DateTime.Today.AddDays(15);
            Assert.InRange(vencimiento, esperado.AddDays(-1), esperado.AddDays(1));
        }

        [Fact]
        public async Task CondicionInexistente_PropagaElMensaje()
        {
            _condiciones.Setup(c => c.ObtenerActivaAsync("XXX")).ThrowsAsync(new Exception("La condición de pago XXX no existe."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _resolutor.ResolverAsync("XXX", null, new DateTime(2026, 1, 10)));

            Assert.Equal("La condición de pago XXX no existe.", ex.Message);
        }

        [Fact]
        public async Task CondicionInactiva_PropagaElMensaje()
        {
            _condiciones.Setup(c => c.ObtenerActivaAsync("C60")).ThrowsAsync(new Exception("La condición de pago C60 está inactiva."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _resolutor.ResolverAsync(null, "C60", new DateTime(2026, 1, 10)));

            Assert.Equal("La condición de pago C60 está inactiva.", ex.Message);
        }
    }
}
