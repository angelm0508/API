using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class SaldoFacturaCompraServiceTests
    {
        private readonly Mock<IRepositorioGenerico<FacturaCompra, int>> _repoFactura = new();
        private readonly Mock<IRepositorioGenerico<NotaCreditoCompra, int>> _repoNota = new();
        private readonly Mock<IRepositorioGenerico<PagoEfectuado, int>> _repoPago = new();
        private readonly Mock<IRepositorioGenerico<PagoEfectuadoFactura, (int Entry, int NoLinea)>> _repoPagoFactura = new();
        private readonly SaldoFacturaCompraService _servicio;

        private readonly List<FacturaCompra> _facturas = new();
        private readonly List<NotaCreditoCompra> _notas = new();
        private readonly List<PagoEfectuado> _pagos = new();
        private readonly List<PagoEfectuadoFactura> _lineas = new();

        public SaldoFacturaCompraServiceTests()
        {
            _servicio = new SaldoFacturaCompraService(_repoFactura.Object, _repoNota.Object, _repoPago.Object, _repoPagoFactura.Object);
            _repoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _facturas.AsAsyncQueryable());
            _repoNota.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _notas.AsAsyncQueryable());
            _repoPago.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _pagos.AsAsyncQueryable());
            _repoPagoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _lineas.AsAsyncQueryable());
        }

        private void FacturaCompra(int entry, decimal total) => _facturas.Add(new FacturaCompra { Entry = entry, NumDoc = entry * 10, TotalDoc = total });

        private void Nota(int baseEntry, decimal total, string cancelado = "N", int baseTipo = 13) =>
            _notas.Add(new NotaCreditoCompra { Entry = _notas.Count + 1, BaseTipo = baseTipo, BaseEntry = baseEntry, TotalDoc = total, Cancelado = cancelado });

        private void Pago(int pagoEntry, int numDoc, int facturaEntry, decimal monto, string cancelado = "N")
        {
            if (_pagos.All(p => p.Entry != pagoEntry))
                _pagos.Add(new PagoEfectuado { Entry = pagoEntry, NumDoc = numDoc, Cancelado = cancelado });
            _lineas.Add(new PagoEfectuadoFactura { Entry = pagoEntry, NoLinea = _lineas.Count + 1, FacturaCompraEntry = facturaEntry, MontoAplicado = monto });
        }

        [Fact]
        public async Task ObtenerAsync_SinPagosNiNotas_SaldoEsElTotalYEstadoPendiente()
        {
            FacturaCompra(1, 100m);

            var r = await _servicio.ObtenerAsync(new[] { 1 });

            var s = r[1];
            Assert.Equal(100m, s.Total);
            Assert.Equal(0m, s.Acreditado);
            Assert.Equal(0m, s.Pagado);
            Assert.Equal(100m, s.Saldo);
            Assert.Equal("Pendiente", s.Estado);
        }

        [Fact]
        public async Task ObtenerAsync_PagoParcial_EstadoParcial()
        {
            FacturaCompra(1, 100m);
            Pago(1, 7, 1, 40m);

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(40m, s.Pagado);
            Assert.Equal(60m, s.Saldo);
            Assert.Equal("Parcial", s.Estado);
        }

        [Fact]
        public async Task ObtenerAsync_PagoTotal_EstadoPagada()
        {
            FacturaCompra(1, 100m);
            Pago(1, 7, 1, 100m);

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(0m, s.Saldo);
            Assert.Equal("Pagada", s.Estado);
        }

        [Fact]
        public async Task ObtenerAsync_PagoCancelado_NoCuenta()
        {
            FacturaCompra(1, 100m);
            Pago(1, 7, 1, 100m, cancelado: "S");

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(0m, s.Pagado);
            Assert.Equal(100m, s.Saldo);
            Assert.Equal("Pendiente", s.Estado);
        }

        [Fact]
        public async Task ObtenerAsync_NotaDeCreditoActivaDescuentaElSaldo_PeroNoLaCancelada()
        {
            FacturaCompra(1, 100m);
            Nota(1, 30m);
            Nota(1, 50m, cancelado: "S");

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(30m, s.Acreditado);
            Assert.Equal(70m, s.Saldo);
            Assert.Equal("Pendiente", s.Estado);
        }

        [Fact]
        public async Task ObtenerAsync_NotaDeCreditoDeOtroTipoBase_NoCuenta()
        {
            FacturaCompra(1, 100m);
            Nota(1, 30m, baseTipo: 6);

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(0m, s.Acreditado);
            Assert.Equal(100m, s.Saldo);
        }

        [Fact]
        public async Task ObtenerAsync_NotaDeCreditoPorElTotal_SaldoCeroYPagada()
        {
            FacturaCompra(1, 100m);
            Nota(1, 100m);

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(0m, s.Saldo);
            Assert.Equal("Pagada", s.Estado);
        }

        [Fact]
        public async Task ObtenerAsync_NotaYPago_RestanAmbos()
        {
            FacturaCompra(1, 100m);
            Nota(1, 20m);
            Pago(1, 7, 1, 30m);

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(50m, s.Saldo);
            Assert.Equal("Parcial", s.Estado);
        }

        [Fact]
        public async Task ObtenerAsync_VariasFacturas_CadaUnaConSuSaldo()
        {
            FacturaCompra(1, 100m);
            FacturaCompra(2, 200m);
            FacturaCompra(3, 300m);
            Pago(1, 7, 1, 100m);
            Pago(2, 8, 2, 50m);

            var r = await _servicio.ObtenerAsync(new[] { 1, 2, 3 });

            Assert.Equal(3, r.Count);
            Assert.Equal("Pagada", r[1].Estado);
            Assert.Equal(150m, r[2].Saldo);
            Assert.Equal(300m, r[3].Saldo);
        }

        [Fact]
        public async Task ObtenerAsync_SoloDevuelveLasPedidas()
        {
            FacturaCompra(1, 100m);
            FacturaCompra(2, 200m);

            var r = await _servicio.ObtenerAsync(new[] { 2 });

            Assert.Single(r);
            Assert.True(r.ContainsKey(2));
        }

        [Fact]
        public async Task ObtenerAsync_FacturaInexistente_NoApareceEnElDiccionario()
        {
            FacturaCompra(1, 100m);

            var r = await _servicio.ObtenerAsync(new[] { 1, 99 });

            Assert.Single(r);
            Assert.False(r.ContainsKey(99));
        }

        [Fact]
        public async Task ObtenerAsync_ListaVacia_DevuelveVacioSinConsultar()
        {
            var r = await _servicio.ObtenerAsync(Array.Empty<int>());

            Assert.Empty(r);
            _repoFactura.Verify(x => x.ObtenerTodoAsync(), Times.Never);
        }

        [Fact]
        public async Task ObtenerAsync_RedondeaADosDecimales()
        {
            FacturaCompra(1, 100.005m);
            Pago(1, 7, 1, 0.004m);

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(100.01m, s.Saldo);   // 100.005 -> 100.01 y 0.004 -> 0.00: cada componente se redondea antes de restar
        }

        [Fact]
        public async Task ObtenerAsync_SaldoNegativoPorDatosExternos_SeInformaCero()
        {
            FacturaCompra(1, 100m);
            Pago(1, 7, 1, 120m);

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(0m, s.Saldo);
            Assert.Equal("Pagada", s.Estado);
        }

        [Fact]
        public async Task ObtenerPagosActivosAsync_SoloLosNoCancelados_ConNumeroYMonto()
        {
            FacturaCompra(1, 100m);
            Pago(1, 7, 1, 30m);
            Pago(2, 8, 1, 20m, cancelado: "S");
            Pago(3, 9, 1, 10m);
            Pago(4, 10, 2, 99m);

            var pagos = await _servicio.ObtenerPagosActivosAsync(1);

            Assert.Equal(new[] { 7, 9 }, pagos.Select(p => p.PagoNumDoc).ToArray());
            Assert.Equal(new[] { 30m, 10m }, pagos.Select(p => p.Monto).ToArray());
        }

        [Fact]
        public async Task ObtenerPagosActivosAsync_SinPagos_DevuelveListaVacia()
        {
            FacturaCompra(1, 100m);

            Assert.Empty(await _servicio.ObtenerPagosActivosAsync(1));
        }

        [Fact]
        public async Task TienePagosRegistradosAsync_ConUnPagoCancelado_DevuelveTrue()
        {
            FacturaCompra(1, 100m);
            Pago(1, 7, 1, 30m, cancelado: "S");

            Assert.True(await _servicio.TienePagosRegistradosAsync(1));
        }

        [Fact]
        public async Task TienePagosRegistradosAsync_ConUnPagoActivo_DevuelveTrue()
        {
            FacturaCompra(1, 100m);
            Pago(1, 7, 1, 30m);

            Assert.True(await _servicio.TienePagosRegistradosAsync(1));
        }

        [Fact]
        public async Task TienePagosRegistradosAsync_SoloPagosDeOtroDocumento_DevuelveFalse()
        {
            FacturaCompra(1, 100m);
            FacturaCompra(2, 100m);
            Pago(1, 7, 2, 30m, cancelado: "S");

            Assert.False(await _servicio.TienePagosRegistradosAsync(1));
        }
    }
}
