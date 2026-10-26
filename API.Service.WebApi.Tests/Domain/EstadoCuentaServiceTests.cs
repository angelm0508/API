using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class EstadoCuentaServiceTests
    {
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocio = new();
        private readonly Mock<IRepositorioGenerico<Factura, int>> _repoFactura = new();
        private readonly Mock<IRepositorioGenerico<NotaCredito, int>> _repoNota = new();
        private readonly Mock<IRepositorioGenerico<PagoRecibido, int>> _repoPago = new();
        private readonly Mock<IRepositorioGenerico<PagoRecibidoFactura, (int Entry, int NoLinea)>> _repoPagoFactura = new();
        private readonly Mock<IRepositorioGenerico<FacturaCompra, int>> _repoCompra = new();
        private readonly Mock<IRepositorioGenerico<NotaCreditoCompra, int>> _repoNotaCompra = new();
        private readonly Mock<IRepositorioGenerico<PagoEfectuado, int>> _repoPagoEfectuado = new();
        private readonly Mock<IRepositorioGenerico<PagoEfectuadoFactura, (int Entry, int NoLinea)>> _repoPagoEfectuadoFactura = new();
        private readonly Mock<ISaldoSocioService> _saldoSocioMock = new();
        private readonly EstadoCuentaService _servicio;
        // Servicio con la cadena real de saldos (para el invariante SaldoFinal == SaldoActual).
        private readonly EstadoCuentaService _servicioReal;

        private readonly List<SocioNegocio> _socios = new();
        private readonly List<Factura> _facturas = new();
        private readonly List<NotaCredito> _notas = new();
        private readonly List<PagoRecibido> _pagos = new();
        private readonly List<PagoRecibidoFactura> _pagoFacturas = new();
        private readonly List<FacturaCompra> _compras = new();
        private readonly List<NotaCreditoCompra> _notasCompra = new();
        private readonly List<PagoEfectuado> _pagosEfectuados = new();
        private readonly List<PagoEfectuadoFactura> _pagoEfectuadoFacturas = new();

        private static readonly DateTime Hoy = DateTime.Today;
        private static readonly DateTime D1 = new(2026, 3, 1);
        private static readonly DateTime D2 = new(2026, 3, 10);
        private static readonly DateTime D3 = new(2026, 3, 20);
        private static readonly DateTime D4 = new(2026, 4, 5);
        private static readonly DateTime Pasado = new(2000, 1, 1);

        public EstadoCuentaServiceTests()
        {
            _repoSocio.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _socios.AsAsyncQueryable());
            _repoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _facturas.AsAsyncQueryable());
            _repoNota.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _notas.AsAsyncQueryable());
            _repoPago.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _pagos.AsAsyncQueryable());
            _repoPagoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _pagoFacturas.AsAsyncQueryable());
            _repoCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _compras.AsAsyncQueryable());
            _repoNotaCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _notasCompra.AsAsyncQueryable());
            _repoPagoEfectuado.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _pagosEfectuados.AsAsyncQueryable());
            _repoPagoEfectuadoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _pagoEfectuadoFacturas.AsAsyncQueryable());

            _saldoSocioMock.Setup(s => s.ObtenerAsync(It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync((IEnumerable<string> codigos) => (IReadOnlyDictionary<string, SaldoSocio>)codigos
                    .ToDictionary(c => c, c => new SaldoSocio(c, "C", 0m, 0m, 0)));

            _servicio = new EstadoCuentaService(_repoSocio.Object, _repoFactura.Object, _repoNota.Object, _repoPago.Object,
                _repoCompra.Object, _repoNotaCompra.Object, _repoPagoEfectuado.Object, _saldoSocioMock.Object);

            var saldoVenta = new SaldoFacturaService(_repoFactura.Object, _repoNota.Object, _repoPago.Object, _repoPagoFactura.Object);
            var saldoCompra = new SaldoFacturaCompraService(_repoCompra.Object, _repoNotaCompra.Object, _repoPagoEfectuado.Object, _repoPagoEfectuadoFactura.Object);
            var saldoSocio = new SaldoSocioService(_repoSocio.Object, _repoFactura.Object, _repoCompra.Object, saldoVenta, saldoCompra);
            _servicioReal = new EstadoCuentaService(_repoSocio.Object, _repoFactura.Object, _repoNota.Object, _repoPago.Object,
                _repoCompra.Object, _repoNotaCompra.Object, _repoPagoEfectuado.Object, saldoSocio);
        }

        private void Socio(string codigo, string tipo) => _socios.Add(new SocioNegocio { Codigo = codigo, Nombre = codigo, TipoSn = tipo });

        private void Fac(int entry, int numDoc, string codigo, decimal total, DateTime fecha, string sub = "--", string cancelado = "N", string? comentario = null, DateTime? vence = null) =>
            _facturas.Add(new Factura { Entry = entry, NumDoc = numDoc, CodigoSn = codigo, DocSubType = sub, Cancelado = cancelado, FechaDoc = fecha, TotalDoc = total, Comentario = comentario, FechaVencimiento = vence ?? fecha });

        private void Nc(int entry, int numDoc, string codigo, decimal total, DateTime fecha, int? baseTipo, int? baseEntry, string cancelado = "N", string? comentario = null) =>
            _notas.Add(new NotaCredito { Entry = entry, NumDoc = numDoc, CodigoSn = codigo, Cancelado = cancelado, FechaDoc = fecha, TotalDoc = total, BaseTipo = baseTipo, BaseEntry = baseEntry, Comentario = comentario });

        // Un pago recibido con una sola línea aplicada a la factura (TotalPago = MontoAplicado, como lo deja PagoRecibidoDomain).
        private void Pago(int entry, int numDoc, string codigo, decimal monto, DateTime fecha, int facturaEntry, string cancelado = "N", string? comentario = null)
        {
            _pagos.Add(new PagoRecibido { Entry = entry, NumDoc = numDoc, CodigoSn = codigo, TotalPago = monto, FechaDoc = fecha, Cancelado = cancelado, Comentario = comentario });
            _pagoFacturas.Add(new PagoRecibidoFactura { Entry = entry, NoLinea = 1, FacturaEntry = facturaEntry, MontoAplicado = monto });
        }

        private void Com(int entry, int numDoc, string codigo, decimal total, DateTime fecha, string sub = "--", string cancelado = "N", string? comentario = null) =>
            _compras.Add(new FacturaCompra { Entry = entry, NumDoc = numDoc, CodigoSn = codigo, DocSubType = sub, Cancelado = cancelado, FechaDoc = fecha, TotalDoc = total, Comentario = comentario, FechaVencimiento = fecha });

        private void NcCompra(int entry, int numDoc, string codigo, decimal total, DateTime fecha, int? baseTipo, int? baseEntry, string cancelado = "N") =>
            _notasCompra.Add(new NotaCreditoCompra { Entry = entry, NumDoc = numDoc, CodigoSn = codigo, Cancelado = cancelado, FechaDoc = fecha, TotalDoc = total, BaseTipo = baseTipo, BaseEntry = baseEntry });

        private void PagoEf(int entry, int numDoc, string codigo, decimal monto, DateTime fecha, int facturaCompraEntry, string cancelado = "N")
        {
            _pagosEfectuados.Add(new PagoEfectuado { Entry = entry, NumDoc = numDoc, CodigoSn = codigo, TotalPago = monto, FechaDoc = fecha, Cancelado = cancelado });
            _pagoEfectuadoFacturas.Add(new PagoEfectuadoFactura { Entry = entry, NoLinea = 1, FacturaCompraEntry = facturaCompraEntry, MontoAplicado = monto });
        }

        [Fact]
        public async Task Cliente_CifraPorCifraDelAcumulado()
        {
            Socio("C1", "C");
            Fac(1, 101, "C1", 1000m, D1, comentario: "Venta de marzo");
            Fac(2, 102, "C1", 200m, D2, sub: "ND");
            Nc(1, 301, "C1", 150m, D3, baseTipo: 6, baseEntry: 1);
            Pago(1, 401, "C1", 300m, D4, facturaEntry: 1);
            Pago(2, 402, "C1", 999m, D4, facturaEntry: 1, cancelado: "S");

            var r = await _servicio.ObtenerAsync("C1", Pasado, D4);

            Assert.Equal(0m, r.SaldoInicial);
            Assert.Equal(4, r.Movimientos.Count);
            Assert.Equal(new MovimientoCuenta(D1, "Factura", 101, "Venta de marzo", 1000m, 0m, 1000m, false), r.Movimientos[0]);
            Assert.Equal(new MovimientoCuenta(D2, "Nota de débito", 102, "Nota de débito 102", 200m, 0m, 1200m, false), r.Movimientos[1]);
            Assert.Equal(new MovimientoCuenta(D3, "Nota de crédito", 301, "Nota de crédito 301", 0m, 150m, 1050m, false), r.Movimientos[2]);
            Assert.Equal(new MovimientoCuenta(D4, "Pago", 401, "Pago 401", 0m, 300m, 750m, false), r.Movimientos[3]);
            Assert.Equal(750m, r.SaldoFinal);
        }

        [Fact]
        public async Task Cliente_SaldoInicialPorMovimientosAnterioresAlRango()
        {
            Socio("C1", "C");
            Fac(1, 101, "C1", 500m, D1);
            Pago(1, 401, "C1", 100m, D1.AddDays(2), facturaEntry: 1);
            Fac(2, 102, "C1", 80m, D3);

            var r = await _servicio.ObtenerAsync("C1", D2, D4);

            Assert.Equal(400m, r.SaldoInicial);
            var m = Assert.Single(r.Movimientos);
            Assert.Equal(102, m.NumDoc);
            Assert.Equal(480m, m.SaldoAcumulado);
            Assert.Equal(480m, r.SaldoFinal);
        }

        [Fact]
        public async Task Cliente_LimitesDelRangoSonInclusivos_YLaHoraNoCuenta()
        {
            Socio("C1", "C");
            Fac(1, 101, "C1", 10m, D2.AddDays(-1).AddHours(23));   // antes de desde
            Fac(2, 102, "C1", 20m, D2.AddHours(15));               // dia desde con hora
            Fac(3, 103, "C1", 30m, D4.AddHours(23));               // dia hasta con hora
            Fac(4, 104, "C1", 40m, D4.AddDays(1));                 // despues de hasta

            var r = await _servicio.ObtenerAsync("C1", D2, D4);

            Assert.Equal(10m, r.SaldoInicial);
            Assert.Equal(new[] { 102, 103 }, r.Movimientos.Select(x => x.NumDoc).ToArray());
            Assert.Equal(D2, r.Movimientos[0].Fecha);
            Assert.Equal(D4, r.Movimientos[1].Fecha);
            Assert.Equal(60m, r.SaldoFinal);
        }

        [Fact]
        public async Task Cliente_OrdenPorFechaLuegoTipoYNumero()
        {
            Socio("C1", "C");
            Pago(1, 401, "C1", 10m, D2, facturaEntry: 9);
            Fac(1, 105, "C1", 100m, D2);
            Fac(2, 103, "C1", 100m, D2);
            Fac(3, 104, "C1", 100m, D2, sub: "ND");
            Fac(4, 102, "C1", 100m, D1);

            var r = await _servicio.ObtenerAsync("C1", Pasado, D4);

            Assert.Equal(
                new[] { "Factura 102", "Factura 103", "Factura 105", "Nota de débito 104", "Pago 401" },
                r.Movimientos.Select(x => $"{x.Tipo} {x.NumDoc}").ToArray());
        }

        [Fact]
        public async Task Cliente_NotaCreditoSinBase_EsInformativaYSinEfecto()
        {
            Socio("C1", "C");
            Fac(1, 101, "C1", 100m, D1);
            Nc(1, 301, "C1", 50m, D2, baseTipo: null, baseEntry: null);
            Nc(2, 302, "C1", 60m, D2, baseTipo: 0, baseEntry: null);
            Nc(3, 303, "C1", 70m, D3, baseTipo: 17, baseEntry: 5);    // base que no es factura
            Nc(4, 304, "C1", 80m, D3, baseTipo: 6, baseEntry: null);  // base incompleta
            Nc(5, 305, "C1", 90m, D1.AddDays(-30), baseTipo: null, baseEntry: null); // informativa anterior al rango

            var r = await _servicio.ObtenerAsync("C1", D1, D4);

            Assert.Equal(0m, r.SaldoInicial);
            Assert.Equal(5, r.Movimientos.Count);
            var informativos = r.Movimientos.Where(m => m.Informativo).ToList();
            Assert.Equal(new[] { 301, 302, 303, 304 }, informativos.Select(m => m.NumDoc).OrderBy(n => n).ToArray());
            Assert.All(informativos, m =>
            {
                Assert.Equal("Nota de crédito", m.Tipo);
                Assert.Equal(0m, m.Cargo);
                Assert.Equal(0m, m.Abono);
                Assert.Equal(100m, m.SaldoAcumulado);
            });
            Assert.Equal(100m, r.SaldoFinal);
        }

        [Fact]
        public async Task Cliente_DocumentosCanceladosQuedanFuera()
        {
            Socio("C1", "C");
            Fac(1, 101, "C1", 100m, D1);
            Fac(2, 102, "C1", 500m, D1, cancelado: "S");
            Fac(3, 103, "C1", 500m, D1, sub: "ND", cancelado: "S");
            Nc(1, 301, "C1", 40m, D2, baseTipo: 6, baseEntry: 1, cancelado: "S");
            Nc(2, 302, "C1", 40m, D2, baseTipo: null, baseEntry: null, cancelado: "S");
            Pago(1, 401, "C1", 70m, D2, facturaEntry: 1, cancelado: "S");

            var r = await _servicio.ObtenerAsync("C1", Pasado, D4);

            var m = Assert.Single(r.Movimientos);
            Assert.Equal(101, m.NumDoc);
            Assert.Equal(100m, r.SaldoFinal);
        }

        [Fact]
        public async Task Cliente_OtrosSubtiposDeFacturaYDocumentosDeOtrosSocios_NoCuentan()
        {
            Socio("C1", "C");
            Socio("C2", "C");
            Fac(1, 101, "C1", 100m, D1);
            Fac(2, 102, "C1", 55m, D1, sub: "NC");
            Fac(3, 103, "C2", 900m, D1);
            Nc(1, 301, "C2", 900m, D1, baseTipo: 6, baseEntry: 3);
            Pago(1, 401, "C2", 5m, D1, facturaEntry: 3);

            var r = await _servicio.ObtenerAsync("C1", Pasado, D4);

            Assert.Equal(new[] { 101 }, r.Movimientos.Select(m => m.NumDoc).ToArray());
            Assert.Equal(100m, r.SaldoFinal);
        }

        [Fact]
        public async Task Cliente_ConsultasAgrupadasUnaPorTabla_YSinTocarCompras()
        {
            Socio("C1", "C");
            Fac(1, 101, "C1", 100m, D1);

            await _servicio.ObtenerAsync("C1", Pasado, D4);

            _repoSocio.Verify(x => x.ObtenerTodoAsync(), Times.Once);
            _repoFactura.Verify(x => x.ObtenerTodoAsync(), Times.Once);
            _repoNota.Verify(x => x.ObtenerTodoAsync(), Times.Once);
            _repoPago.Verify(x => x.ObtenerTodoAsync(), Times.Once);
            _repoCompra.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _repoNotaCompra.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _repoPagoEfectuado.Verify(x => x.ObtenerTodoAsync(), Times.Never);
        }

        [Fact]
        public async Task Cliente_RedondeaCargoYAbonoADosDecimales_YAcumulaLoRedondeado()
        {
            Socio("C1", "C");
            Fac(1, 101, "C1", 10.005m, D1);   // 10.01
            Fac(2, 102, "C1", 10.004m, D2);   // 10.00
            Nc(1, 301, "C1", 0.015m, D3, baseTipo: 6, baseEntry: 1); // 0.02

            var r = await _servicio.ObtenerAsync("C1", Pasado, D4);

            Assert.Equal(10.01m, r.Movimientos[0].Cargo);
            Assert.Equal(10.01m, r.Movimientos[0].SaldoAcumulado);
            Assert.Equal(10.00m, r.Movimientos[1].Cargo);
            Assert.Equal(20.01m, r.Movimientos[1].SaldoAcumulado);
            Assert.Equal(0.02m, r.Movimientos[2].Abono);
            Assert.Equal(19.99m, r.Movimientos[2].SaldoAcumulado);
            Assert.Equal(19.99m, r.SaldoFinal);
        }

        [Fact]
        public async Task Cliente_SinMovimientos_SaldoFinalEsElInicial()
        {
            Socio("C1", "C");
            Fac(1, 101, "C1", 300m, D1);

            var r = await _servicio.ObtenerAsync("C1", D3, D4);

            Assert.Equal(300m, r.SaldoInicial);
            Assert.Empty(r.Movimientos);
            Assert.Equal(300m, r.SaldoFinal);
        }

        [Fact]
        public async Task Cliente_DescripcionEsElComentarioRecortadoOElNombreDelDocumento()
        {
            Socio("C1", "C");
            Fac(1, 101, "C1", 10m, D1, comentario: "  Venta mostrador  ");
            Fac(2, 102, "C1", 10m, D1, comentario: "   ");
            Fac(3, 103, "C1", 10m, D1, comentario: new string('x', 300));
            Pago(1, 401, "C1", 5m, D2, facturaEntry: 1, comentario: "Deposito BAC");

            var r = await _servicio.ObtenerAsync("C1", Pasado, D4);

            Assert.Equal("Venta mostrador", r.Movimientos.Single(m => m.NumDoc == 101).Descripcion);
            Assert.Equal("Factura 102", r.Movimientos.Single(m => m.NumDoc == 102).Descripcion);
            Assert.Equal(100, r.Movimientos.Single(m => m.NumDoc == 103).Descripcion.Length);
            Assert.Equal("Deposito BAC", r.Movimientos.Single(m => m.NumDoc == 401).Descripcion);
        }

        [Fact]
        public async Task Proveedor_CifraPorCifraDelAcumulado()
        {
            Socio("P1", "P");
            Com(1, 201, "P1", 800m, D1, comentario: "Compra de marzo");
            Com(2, 202, "P1", 100m, D2, sub: "DN");
            NcCompra(1, 501, "P1", 60m, D3, baseTipo: 13, baseEntry: 1);
            PagoEf(1, 601, "P1", 250m, D4, facturaCompraEntry: 1);
            PagoEf(2, 602, "P1", 999m, D4, facturaCompraEntry: 1, cancelado: "S");

            var r = await _servicio.ObtenerAsync("P1", Pasado, D4);

            Assert.Equal(0m, r.SaldoInicial);
            Assert.Equal(4, r.Movimientos.Count);
            Assert.Equal(new MovimientoCuenta(D1, "Factura de compra", 201, "Compra de marzo", 800m, 0m, 800m, false), r.Movimientos[0]);
            Assert.Equal(new MovimientoCuenta(D2, "Nota de débito de proveedor", 202, "Nota de débito de proveedor 202", 100m, 0m, 900m, false), r.Movimientos[1]);
            Assert.Equal(new MovimientoCuenta(D3, "Nota de crédito de proveedor", 501, "Nota de crédito de proveedor 501", 0m, 60m, 840m, false), r.Movimientos[2]);
            Assert.Equal(new MovimientoCuenta(D4, "Pago efectuado", 601, "Pago efectuado 601", 0m, 250m, 590m, false), r.Movimientos[3]);
            Assert.Equal(590m, r.SaldoFinal);
        }

        [Fact]
        public async Task Proveedor_SaldoInicial_Informativas_CanceladosYConsultas()
        {
            Socio("P1", "P");
            Com(1, 201, "P1", 500m, D1);
            PagoEf(1, 601, "P1", 100m, D1.AddDays(2), facturaCompraEntry: 1);
            Com(2, 202, "P1", 999m, D1, cancelado: "S");
            Com(3, 203, "P1", 80m, D3);
            NcCompra(1, 501, "P1", 40m, D3, baseTipo: null, baseEntry: null);
            NcCompra(2, 502, "P1", 40m, D3, baseTipo: 6, baseEntry: 1);   // BaseTipo de venta: no es factura de compra
            Fac(1, 101, "P1", 700m, D3);                                   // tabla de ventas: no se consulta

            var r = await _servicio.ObtenerAsync("P1", D2, D4);

            Assert.Equal(400m, r.SaldoInicial);
            Assert.Equal(3, r.Movimientos.Count);
            Assert.Equal(480m, r.SaldoFinal);
            Assert.Equal(2, r.Movimientos.Count(m => m.Informativo));
            _repoFactura.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _repoNota.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _repoPago.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _repoCompra.Verify(x => x.ObtenerTodoAsync(), Times.Once);
            _repoNotaCompra.Verify(x => x.ObtenerTodoAsync(), Times.Once);
            _repoPagoEfectuado.Verify(x => x.ObtenerTodoAsync(), Times.Once);
        }

        [Fact]
        public async Task RangoPorDefecto_UnoDeEneroAHoy()
        {
            Socio("C1", "C");
            var enero1 = new DateTime(Hoy.Year, 1, 1);
            Fac(1, 101, "C1", 100m, enero1.AddDays(-1));  // año anterior: saldo inicial
            Fac(2, 102, "C1", 50m, enero1);               // primer dia del rango
            Fac(3, 103, "C1", 25m, Hoy);                  // hoy
            Fac(4, 104, "C1", 999m, Hoy.AddDays(1));      // futuro: fuera

            var r = await _servicio.ObtenerAsync("C1", null, null);

            Assert.Equal(enero1, r.Desde);
            Assert.Equal(Hoy, r.Hasta);
            Assert.Equal(100m, r.SaldoInicial);
            Assert.Equal(new[] { 102, 103 }, r.Movimientos.Select(m => m.NumDoc).ToArray());
            Assert.Equal(175m, r.SaldoFinal);
        }

        [Fact]
        public async Task DesdePosteriorAHasta_Lanza()
        {
            Socio("C1", "C");

            var ex = await Assert.ThrowsAsync<Exception>(() => _servicio.ObtenerAsync("C1", D4, D1));

            Assert.Equal("La fecha inicial no puede ser posterior a la final.", ex.Message);
        }

        [Fact]
        public async Task DesdeIgualAHastaConHoraDistinta_NoLanza()
        {
            Socio("C1", "C");
            Fac(1, 101, "C1", 10m, D2);

            var r = await _servicio.ObtenerAsync("C1", D2.AddHours(20), D2.AddHours(1));

            Assert.Single(r.Movimientos);
        }

        [Fact]
        public async Task SoloDesdePosteriorAHoy_UsaHoyComoHasta_YLanza()
        {
            Socio("C1", "C");

            var ex = await Assert.ThrowsAsync<Exception>(() => _servicio.ObtenerAsync("C1", Hoy.AddDays(3), null));

            Assert.Equal("La fecha inicial no puede ser posterior a la final.", ex.Message);
        }

        [Fact]
        public async Task SocioInexistente_Lanza()
        {
            Socio("C1", "C");

            var ex = await Assert.ThrowsAsync<Exception>(() => _servicio.ObtenerAsync("NOEXISTE", null, null));
            var ex2 = await Assert.ThrowsAsync<Exception>(() => _servicio.ObtenerAsync("", null, null));

            Assert.Equal("El socio de negocio no existe.", ex.Message);
            Assert.Equal("El socio de negocio no existe.", ex2.Message);
        }

        [Fact]
        public async Task DevuelveElSocioYElSaldoActualYVencidoDelServicioDeSaldos()
        {
            Socio("C1", "C");
            _saldoSocioMock.Setup(s => s.ObtenerAsync(It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(new Dictionary<string, SaldoSocio> { ["C1"] = new SaldoSocio("C1", "C", 321.45m, 100m, 2) });

            var r = await _servicio.ObtenerAsync("C1", D1, D4);

            Assert.Equal("C1", r.Socio.Codigo);
            Assert.Equal(321.45m, r.SaldoActual);
            Assert.Equal(100m, r.Vencido);
            _saldoSocioMock.Verify(s => s.ObtenerAsync(It.Is<IEnumerable<string>>(c => c.Single() == "C1")), Times.Once);
        }

        [Fact]
        public async Task SocioSinSaldoEnElServicio_SaldoActualYVencidoEnCero()
        {
            Socio("C1", "C");
            _saldoSocioMock.Setup(s => s.ObtenerAsync(It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(new Dictionary<string, SaldoSocio>());

            var r = await _servicio.ObtenerAsync("C1", D1, D4);

            Assert.Equal(0m, r.SaldoActual);
            Assert.Equal(0m, r.Vencido);
        }

        [Fact]
        public async Task Invariante_Cliente_SaldoFinalIgualASaldoActual_CasoMixto()
        {
            Socio("C1", "C");
            Fac(1, 101, "C1", 1000m, Hoy.AddDays(-90), vence: Hoy.AddDays(-60));         // con NC y pago parcial
            Fac(2, 102, "C1", 250.55m, Hoy.AddDays(-50), sub: "ND", vence: Hoy.AddDays(-20));
            Fac(3, 103, "C1", 400m, Hoy.AddDays(-30), vence: Hoy.AddDays(15));            // saldada
            Fac(4, 104, "C1", 700m, Hoy.AddDays(-20), cancelado: "S");                    // cancelada
            Nc(1, 301, "C1", 150m, Hoy.AddDays(-80), baseTipo: 6, baseEntry: 1);
            Nc(2, 302, "C1", 55m, Hoy.AddDays(-75), baseTipo: null, baseEntry: null);     // informativa
            Nc(3, 303, "C1", 90m, Hoy.AddDays(-70), baseTipo: 6, baseEntry: 1, cancelado: "S");
            Pago(1, 401, "C1", 300m, Hoy.AddDays(-60), facturaEntry: 1);
            Pago(2, 402, "C1", 400m, Hoy.AddDays(-25), facturaEntry: 3);
            Pago(3, 403, "C1", 50m, Hoy.AddDays(-10), facturaEntry: 2, cancelado: "S");
            // Documentos de otro socio que no deben mezclarse.
            Socio("C2", "C");
            Fac(5, 105, "C2", 9999m, Hoy.AddDays(-5));

            var r = await _servicioReal.ObtenerAsync("C1", Pasado, Hoy);

            // 1000 - 150 - 300 = 550 ; 250.55 ; 400 - 400 = 0
            Assert.Equal(800.55m, r.SaldoActual);
            Assert.Equal(r.SaldoActual, r.SaldoFinal);
        }

        [Fact]
        public async Task Invariante_Proveedor_SaldoFinalIgualASaldoActual_CasoMixto()
        {
            Socio("P1", "P");
            Com(1, 201, "P1", 2000m, Hoy.AddDays(-90));
            Com(2, 202, "P1", 120m, Hoy.AddDays(-40), sub: "DN");
            Com(3, 203, "P1", 300m, Hoy.AddDays(-30));
            Com(4, 204, "P1", 700m, Hoy.AddDays(-20), cancelado: "S");
            NcCompra(1, 501, "P1", 500m, Hoy.AddDays(-80), baseTipo: 13, baseEntry: 1);
            NcCompra(2, 502, "P1", 35m, Hoy.AddDays(-70), baseTipo: null, baseEntry: null);
            PagoEf(1, 601, "P1", 600m, Hoy.AddDays(-60), facturaCompraEntry: 1);
            PagoEf(2, 602, "P1", 300m, Hoy.AddDays(-25), facturaCompraEntry: 3);
            PagoEf(3, 603, "P1", 77m, Hoy.AddDays(-10), facturaCompraEntry: 2, cancelado: "S");

            var r = await _servicioReal.ObtenerAsync("P1", Pasado, Hoy);

            // 2000 - 500 - 600 = 900 ; 120 ; 300 - 300 = 0
            Assert.Equal(1020m, r.SaldoActual);
            Assert.Equal(r.SaldoActual, r.SaldoFinal);
        }
    }
}
