using API.Domain.Core;
using API.Domain.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class GuardaPagosTests
    {
        private readonly Mock<ISaldoFacturaService> _saldos = new();

        [Fact]
        public async Task ValidarSinPagosAsync_SinPagos_NoLanza()
        {
            _saldos.Setup(s => s.ObtenerPagosActivosAsync(7)).ReturnsAsync(new List<PagoAplicado>());

            await GuardaPagos.ValidarSinPagosAsync(_saldos.Object, 7);
        }

        [Fact]
        public async Task ValidarSinPagosAsync_UnPago_LoNombra()
        {
            _saldos.Setup(s => s.ObtenerPagosActivosAsync(7)).ReturnsAsync(new List<PagoAplicado> { new(1, 3, 40m) });

            var ex = await Assert.ThrowsAsync<Exception>(() => GuardaPagos.ValidarSinPagosAsync(_saldos.Object, 7));

            Assert.Equal("El documento tiene pagos aplicados (Pago 3). Cancele primero el pago.", ex.Message);
        }

        [Fact]
        public async Task ValidarSinPagosAsync_VariosPagos_LosListaPorNumero()
        {
            _saldos.Setup(s => s.ObtenerPagosActivosAsync(7)).ReturnsAsync(new List<PagoAplicado> { new(1, 3, 40m), new(2, 5, 60m) });

            var ex = await Assert.ThrowsAsync<Exception>(() => GuardaPagos.ValidarSinPagosAsync(_saldos.Object, 7));

            Assert.Equal("El documento tiene pagos aplicados (Pago 3, Pago 5). Cancele primero el pago.", ex.Message);
        }
    }
}
