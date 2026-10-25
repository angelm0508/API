using API.Domain.Core;
using API.Domain.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class GuardaPagosEfectuadosTests
    {
        private readonly Mock<ISaldoFacturaCompraService> _saldos = new();

        [Fact]
        public async Task ValidarSinPagosAsync_SinPagos_NoLanza()
        {
            _saldos.Setup(s => s.ObtenerPagosActivosAsync(7)).ReturnsAsync(new List<PagoAplicado>());

            await GuardaPagosEfectuados.ValidarSinPagosAsync(_saldos.Object, 7);
        }

        [Fact]
        public async Task ValidarSinPagosAsync_UnPago_LoNombra()
        {
            _saldos.Setup(s => s.ObtenerPagosActivosAsync(7)).ReturnsAsync(new List<PagoAplicado> { new(1, 3, 40m) });

            var ex = await Assert.ThrowsAsync<Exception>(() => GuardaPagosEfectuados.ValidarSinPagosAsync(_saldos.Object, 7));

            Assert.Equal("El documento tiene pagos aplicados (Pago 3). Cancele primero el pago.", ex.Message);
        }

        [Fact]
        public async Task ValidarSinPagosAsync_VariosPagos_LosListaPorNumero()
        {
            _saldos.Setup(s => s.ObtenerPagosActivosAsync(7)).ReturnsAsync(new List<PagoAplicado> { new(1, 3, 40m), new(2, 5, 60m) });

            var ex = await Assert.ThrowsAsync<Exception>(() => GuardaPagosEfectuados.ValidarSinPagosAsync(_saldos.Object, 7));

            Assert.Equal("El documento tiene pagos aplicados (Pago 3, Pago 5). Cancele primero el pago.", ex.Message);
        }

        [Fact]
        public async Task ValidarSinHistorialAsync_SinPagosRegistrados_NoLanza()
        {
            _saldos.Setup(s => s.TienePagosRegistradosAsync(7)).ReturnsAsync(false);

            await GuardaPagosEfectuados.ValidarSinHistorialAsync(_saldos.Object, 7);
        }

        [Fact]
        public async Task ValidarSinHistorialAsync_ConPagosRegistrados_Lanza()
        {
            _saldos.Setup(s => s.TienePagosRegistradosAsync(7)).ReturnsAsync(true);

            var ex = await Assert.ThrowsAsync<Exception>(() => GuardaPagosEfectuados.ValidarSinHistorialAsync(_saldos.Object, 7));

            Assert.Equal("El documento tiene pagos registrados (cancelados) y no se puede eliminar; consérvelo cancelado.", ex.Message);
        }
    }
}
