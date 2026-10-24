using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class SaldoFacturaServiceTests
    {
        private readonly Mock<IRepositorioGenerico<Factura, int>> _repoFactura = new();
        private readonly Mock<IRepositorioGenerico<NotaCredito, int>> _repoNota = new();
        private readonly Mock<IRepositorioGenerico<PagoRecibido, int>> _repoPago = new();
        private readonly Mock<IRepositorioGenerico<PagoRecibidoFactura, (int Entry, int NoLinea)>> _repoPagoFactura = new();
        private readonly SaldoFacturaService _servicio;

        private readonly List<Factura> _facturas = new();
        private readonly List<NotaCredito> _notas = new();
        private readonly List<PagoRecibido> _pagos = new();
        private readonly List<PagoRecibidoFactura> _lineas = new();

        public SaldoFacturaServiceTests()
        {
            _servicio = new SaldoFacturaService(_repoFactura.Object, _repoNota.Object, _repoPago.Object, _repoPagoFactura.Object);
            _repoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _facturas.AsAsyncQueryable());
            _repoNota.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _notas.AsAsyncQueryable());
            _repoPago.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _pagos.AsAsyncQueryable());
            _repoPagoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _lineas.AsAsyncQueryable());
        }

        private void Factura(int entry, decimal total) => _facturas.Add(new Factura { Entry = entry, NumDoc = entry * 10, TotalDoc = total });

        private void Nota(int baseEntry, decimal total, string cancelado = "N", int baseTipo = 6) =>
            _notas.Add(new NotaCredito { Entry = _notas.Count + 1, BaseTipo = baseTipo, BaseEntry = baseEntry, TotalDoc = total, Cancelado = cancelado });

        private void Pago(int pagoEntry, int numDoc, int facturaEntry, decimal monto, string cancelado = "N")
        {
            if (_pagos.All(p => p.Entry != pagoEntry))
                _pagos.Add(new PagoRecibido { Entry = pagoEntry, NumDoc = numDoc, Cancelado = cancelado });
            _lineas.Add(new PagoRecibidoFactura { Entry = pagoEntry, NoLinea = _lineas.Count + 1, FacturaEntry = facturaEntry, MontoAplicado = monto });
        }

        [Fact]
        public async Task ObtenerAsync_SinPagosNiNotas_SaldoEsElTotalYEstadoPendiente()
        {
            Factura(1, 100m);

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
            Factura(1, 100m);
            Pago(1, 7, 1, 40m);

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(40m, s.Pagado);
            Assert.Equal(60m, s.Saldo);
            Assert.Equal("Parcial", s.Estado);
        }

        [Fact]
        public async Task ObtenerAsync_PagoTotal_EstadoPagada()
        {
            Factura(1, 100m);
            Pago(1, 7, 1, 100m);

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(0m, s.Saldo);
            Assert.Equal("Pagada", s.Estado);
        }

        [Fact]
        public async Task ObtenerAsync_PagoCancelado_NoCuenta()
        {
            Factura(1, 100m);
            Pago(1, 7, 1, 100m, cancelado: "S");

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(0m, s.Pagado);
            Assert.Equal(100m, s.Saldo);
            Assert.Equal("Pendiente", s.Estado);
        }

        [Fact]
        public async Task ObtenerAsync_NotaDeCreditoActivaDescuentaElSaldo_PeroNoLaCancelada()
        {
            Factura(1, 100m);
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
            Factura(1, 100m);
            Nota(1, 30m, baseTipo: 5);

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(0m, s.Acreditado);
            Assert.Equal(100m, s.Saldo);
        }

        [Fact]
        public async Task ObtenerAsync_NotaDeCreditoPorElTotal_SaldoCeroYPagada()
        {
            Factura(1, 100m);
            Nota(1, 100m);

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(0m, s.Saldo);
            Assert.Equal("Pagada", s.Estado);
        }

        [Fact]
        public async Task ObtenerAsync_NotaYPago_RestanAmbos()
        {
            Factura(1, 100m);
            Nota(1, 20m);
            Pago(1, 7, 1, 30m);

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(50m, s.Saldo);
            Assert.Equal("Parcial", s.Estado);
        }

        [Fact]
        public async Task ObtenerAsync_VariasFacturas_CadaUnaConSuSaldo()
        {
            Factura(1, 100m);
            Factura(2, 200m);
            Factura(3, 300m);
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
            Factura(1, 100m);
            Factura(2, 200m);

            var r = await _servicio.ObtenerAsync(new[] { 2 });

            Assert.Single(r);
            Assert.True(r.ContainsKey(2));
        }

        [Fact]
        public async Task ObtenerAsync_FacturaInexistente_NoApareceEnElDiccionario()
        {
            Factura(1, 100m);

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
            Factura(1, 100.005m);
            Pago(1, 7, 1, 0.004m);

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(100.01m, s.Saldo);   // 100.005 -> 100.01 y 0.004 -> 0.00: cada componente se redondea antes de restar
        }

        [Fact]
        public async Task ObtenerAsync_SaldoNegativoPorDatosExternos_SeInformaCero()
        {
            Factura(1, 100m);
            Pago(1, 7, 1, 120m);

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(0m, s.Saldo);
            Assert.Equal("Pagada", s.Estado);
        }

        [Fact]
        public async Task ObtenerPagosActivosAsync_SoloLosNoCancelados_ConNumeroYMonto()
        {
            Factura(1, 100m);
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
            Factura(1, 100m);

            Assert.Empty(await _servicio.ObtenerPagosActivosAsync(1));
        }
    }
}
