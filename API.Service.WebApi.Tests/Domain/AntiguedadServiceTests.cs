using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class AntiguedadServiceTests
    {
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocio = new();
        private readonly Mock<IRepositorioGenerico<Factura, int>> _repoFactura = new();
        private readonly Mock<IRepositorioGenerico<FacturaCompra, int>> _repoCompra = new();
        private readonly Mock<ISaldoFacturaService> _saldoVenta = new();
        private readonly Mock<ISaldoFacturaCompraService> _saldoCompra = new();
        private readonly AntiguedadService _servicio;

        private readonly List<SocioNegocio> _socios = new();
        private readonly List<Factura> _facturas = new();
        private readonly List<FacturaCompra> _compras = new();
        // Saldo por entry que devuelven los servicios de saldo de documento.
        private readonly Dictionary<int, decimal> _saldosVenta = new();
        private readonly Dictionary<int, decimal> _saldosCompra = new();
        // Entries recibidos por cada llamada a los servicios de saldo.
        private readonly List<List<int>> _llamadasVenta = new();
        private readonly List<List<int>> _llamadasCompra = new();

        private static readonly DateTime Hoy = DateTime.Today;

        public AntiguedadServiceTests()
        {
            _servicio = new AntiguedadService(_repoSocio.Object, _repoFactura.Object, _repoCompra.Object, _saldoVenta.Object, _saldoCompra.Object);
            _repoSocio.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _socios.AsAsyncQueryable());
            _repoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _facturas.AsAsyncQueryable());
            _repoCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _compras.AsAsyncQueryable());
            _saldoVenta.Setup(s => s.ObtenerAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync((IEnumerable<int> ids) =>
                {
                    var lista = ids.ToList();
                    _llamadasVenta.Add(lista);
                    return (IReadOnlyDictionary<int, SaldoFactura>)lista
                        .Where(_saldosVenta.ContainsKey)
                        .ToDictionary(i => i, i => new SaldoFactura(i, _saldosVenta[i], 0m, 0m, _saldosVenta[i], "Pendiente"));
                });
            _saldoCompra.Setup(s => s.ObtenerAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync((IEnumerable<int> ids) =>
                {
                    var lista = ids.ToList();
                    _llamadasCompra.Add(lista);
                    return (IReadOnlyDictionary<int, SaldoFacturaCompra>)lista
                        .Where(_saldosCompra.ContainsKey)
                        .ToDictionary(i => i, i => new SaldoFacturaCompra(i, _saldosCompra[i], 0m, 0m, _saldosCompra[i], "Pendiente"));
                });
        }

        private void Socio(string codigo, string tipo, string? nombre = null) =>
            _socios.Add(new SocioNegocio { Codigo = codigo, Nombre = nombre ?? "Nombre " + codigo, TipoSn = tipo });

        // El vencimiento se expresa en días de vencido: hoy - dias.
        private void Fac(int entry, string codigo, decimal saldo, int diasVencido, string sub = "--", string cancelado = "N", int numDoc = 0)
        {
            _facturas.Add(new Factura { Entry = entry, NumDoc = numDoc == 0 ? entry : numDoc, CodigoSn = codigo, DocSubType = sub, Cancelado = cancelado, FechaDoc = Hoy.AddDays(-diasVencido - 20), FechaVencimiento = Hoy.AddDays(-diasVencido) });
            _saldosVenta[entry] = saldo;
        }

        private void Com(int entry, string codigo, decimal saldo, int diasVencido, string sub = "--", string cancelado = "N", int numDoc = 0)
        {
            _compras.Add(new FacturaCompra { Entry = entry, NumDoc = numDoc == 0 ? entry : numDoc, CodigoSn = codigo, DocSubType = sub, Cancelado = cancelado, FechaDoc = Hoy.AddDays(-diasVencido - 20), FechaVencimiento = Hoy.AddDays(-diasVencido) });
            _saldosCompra[entry] = saldo;
        }

        private async Task<FilaAntiguedad> FilaCliente(string codigo) =>
            Assert.Single(await _servicio.ObtenerAsync("C"), f => f.CodigoSn == codigo);

        // ---- Cortes de tramo (límites exactos) ----

        [Theory]
        [InlineData(-5, 1, 0, 0, 0, 0)]
        [InlineData(0, 1, 0, 0, 0, 0)]
        [InlineData(1, 0, 1, 0, 0, 0)]
        [InlineData(30, 0, 1, 0, 0, 0)]
        [InlineData(31, 0, 0, 1, 0, 0)]
        [InlineData(60, 0, 0, 1, 0, 0)]
        [InlineData(61, 0, 0, 0, 1, 0)]
        [InlineData(90, 0, 0, 0, 1, 0)]
        [InlineData(91, 0, 0, 0, 0, 1)]
        [InlineData(400, 0, 0, 0, 0, 1)]
        public async Task ObtenerAsync_Cliente_CorteDeTramoEnLosLimites(int dias, decimal vigente, decimal d1a30, decimal d31a60, decimal d61a90, decimal mas90)
        {
            Socio("C1", "C");
            Fac(1, "C1", 100m, dias);

            var f = await FilaCliente("C1");

            Assert.Equal(vigente * 100m, f.Vigente);
            Assert.Equal(d1a30 * 100m, f.De1a30);
            Assert.Equal(d31a60 * 100m, f.De31a60);
            Assert.Equal(d61a90 * 100m, f.De61a90);
            Assert.Equal(mas90 * 100m, f.Mas90);
            Assert.Equal(100m, f.Total);
        }

        [Theory]
        [InlineData(0, 1, 0, 0, 0, 0)]
        [InlineData(1, 0, 1, 0, 0, 0)]
        [InlineData(30, 0, 1, 0, 0, 0)]
        [InlineData(31, 0, 0, 1, 0, 0)]
        [InlineData(60, 0, 0, 1, 0, 0)]
        [InlineData(61, 0, 0, 0, 1, 0)]
        [InlineData(90, 0, 0, 0, 1, 0)]
        [InlineData(91, 0, 0, 0, 0, 1)]
        public async Task ObtenerAsync_Proveedor_CorteDeTramoEnLosLimites(int dias, decimal vigente, decimal d1a30, decimal d31a60, decimal d61a90, decimal mas90)
        {
            Socio("P1", "P");
            Com(1, "P1", 100m, dias);

            var f = Assert.Single(await _servicio.ObtenerAsync("P"));

            Assert.Equal("P1", f.CodigoSn);
            Assert.Equal(vigente * 100m, f.Vigente);
            Assert.Equal(d1a30 * 100m, f.De1a30);
            Assert.Equal(d31a60 * 100m, f.De31a60);
            Assert.Equal(d61a90 * 100m, f.De61a90);
            Assert.Equal(mas90 * 100m, f.Mas90);
            Assert.Equal(100m, f.Total);
        }

        // ---- Suma por socio ----

        [Fact]
        public async Task ObtenerAsync_SocioConDocumentosEnVariosTramos_SumaCadaTramoYElTotal()
        {
            Socio("C1", "C", "Ana");
            Fac(1, "C1", 10m, -3);    // vigente
            Fac(2, "C1", 20m, 0);     // vigente
            Fac(3, "C1", 30m, 15);    // 1-30
            Fac(4, "C1", 40m, 45);    // 31-60
            Fac(5, "C1", 50m, 75);    // 61-90
            Fac(6, "C1", 60m, 120);   // > 90
            Fac(7, "C1", 5.5m, 120);  // > 90

            var f = await FilaCliente("C1");

            Assert.Equal(new FilaAntiguedad("C1", "Ana", 30m, 30m, 40m, 50m, 65.5m, 215.5m), f);
        }

        [Fact]
        public async Task ObtenerAsync_PagoParcial_ReduceElSaldoDelTramo()
        {
            Socio("C1", "C");
            Fac(1, "C1", 100m, 40);
            _saldosVenta[1] = 35.25m; // el servicio de saldo ya descontó pagos y notas de crédito

            var f = await FilaCliente("C1");

            Assert.Equal(35.25m, f.De31a60);
            Assert.Equal(35.25m, f.Total);
        }

        [Fact]
        public async Task ObtenerAsync_NotaDebito_EntraEnLaAntiguedad()
        {
            Socio("C1", "C");
            Socio("P1", "P");
            Fac(1, "C1", 30m, 10, sub: "ND");
            Com(10, "P1", 45m, 10, sub: "DN");

            Assert.Equal(30m, (await FilaCliente("C1")).De1a30);
            Assert.Equal(45m, Assert.Single(await _servicio.ObtenerAsync("P")).De1a30);
        }

        [Fact]
        public async Task ObtenerAsync_DocumentoSinSaldo_QuedaFuera_YSocioSinSaldoNoAparece()
        {
            Socio("C1", "C");
            Socio("C2", "C");
            Fac(1, "C1", 0m, 50);     // pagada
            Fac(2, "C2", 70m, 50);

            var r = await _servicio.ObtenerAsync("C");

            var f = Assert.Single(r);
            Assert.Equal("C2", f.CodigoSn);
            Assert.Equal(70m, f.Total);
        }

        [Fact]
        public async Task ObtenerAsync_SaldoNegativoOInexistenteEnElServicio_QuedaFuera()
        {
            Socio("C1", "C");
            Fac(1, "C1", -10m, 50);
            _facturas.Add(new Factura { Entry = 2, NumDoc = 2, CodigoSn = "C1", DocSubType = "--", Cancelado = "N", FechaDoc = Hoy, FechaVencimiento = Hoy }); // sin saldo calculado

            Assert.Empty(await _servicio.ObtenerAsync("C"));
        }

        [Fact]
        public async Task ObtenerAsync_DocumentoCancelado_QuedaFuera_YNoSeEnviaAlServicioDeSaldo()
        {
            Socio("C1", "C");
            Socio("P1", "P");
            Fac(1, "C1", 100m, 10, cancelado: "S");
            Fac(2, "C1", 25m, 10);
            Com(10, "P1", 100m, 10, cancelado: "S");

            var r = await _servicio.ObtenerAsync("C");
            var rp = await _servicio.ObtenerAsync("P");

            Assert.Equal(25m, Assert.Single(r).Total);
            Assert.Empty(rp);
            Assert.Equal(new[] { 2 }, Assert.Single(_llamadasVenta));
        }

        [Fact]
        public async Task ObtenerAsync_SubtipoDistintoDeFacturaONotaDebito_NoCuenta()
        {
            Socio("C1", "C");
            Fac(1, "C1", 30m, 10, sub: "NC");

            Assert.Empty(await _servicio.ObtenerAsync("C"));
        }

        [Fact]
        public async Task ObtenerAsync_VencimientoNulo_UsaFechaDoc()
        {
            Socio("C1", "C");
            _facturas.Add(new Factura { Entry = 1, NumDoc = 1, CodigoSn = "C1", DocSubType = "--", Cancelado = "N", FechaDoc = Hoy.AddDays(-31), FechaVencimiento = null });
            _facturas.Add(new Factura { Entry = 2, NumDoc = 2, CodigoSn = "C1", DocSubType = "--", Cancelado = "N", FechaDoc = Hoy.AddDays(-30).AddHours(15), FechaVencimiento = null });
            _saldosVenta[1] = 60m;
            _saldosVenta[2] = 40m;

            var f = await FilaCliente("C1");

            Assert.Equal(60m, f.De31a60);
            Assert.Equal(40m, f.De1a30);
        }

        [Fact]
        public async Task ObtenerAsync_IgnoraLaHoraDelVencimiento()
        {
            Socio("C1", "C");
            _facturas.Add(new Factura { Entry = 1, NumDoc = 1, CodigoSn = "C1", DocSubType = "--", Cancelado = "N", FechaDoc = Hoy.AddDays(-5), FechaVencimiento = Hoy.AddHours(23).AddMinutes(59) });
            _saldosVenta[1] = 10m;

            var f = await FilaCliente("C1");

            Assert.Equal(10m, f.Vigente);
        }

        [Fact]
        public async Task ObtenerAsync_SoloDocumentosDelTipoPedido()
        {
            Socio("C1", "C");
            Socio("P1", "P");
            Fac(1, "C1", 100m, 10);
            Com(10, "P1", 70m, 10);

            var cobrar = await _servicio.ObtenerAsync("C");
            var pagar = await _servicio.ObtenerAsync("P");

            Assert.Equal("C1", Assert.Single(cobrar).CodigoSn);
            Assert.Equal("P1", Assert.Single(pagar).CodigoSn);
        }

        [Fact]
        public async Task ObtenerAsync_RedondeaCadaSaldoYSumaLosRedondeados()
        {
            Socio("C1", "C");
            Fac(1, "C1", 0.005m, 10);   // 0.01
            Fac(2, "C1", 0.004m, 10);   // 0.00: no aporta
            Fac(3, "C1", 10.125m, 10);  // 10.13

            var f = await FilaCliente("C1");

            Assert.Equal(10.14m, f.De1a30);
            Assert.Equal(10.14m, f.Total);
        }

        [Fact]
        public async Task ObtenerAsync_SocioSoloConSaldoMinimoQueRedondeaACero_NoAparece()
        {
            Socio("C1", "C");
            Fac(1, "C1", 0.004m, 10);

            Assert.Empty(await _servicio.ObtenerAsync("C"));
        }

        [Fact]
        public async Task ObtenerAsync_TotalEsLaSumaDeLosCincoTramos()
        {
            Socio("C1", "C");
            Fac(1, "C1", 0.33m, 0);
            Fac(2, "C1", 0.33m, 5);
            Fac(3, "C1", 0.33m, 35);
            Fac(4, "C1", 0.33m, 65);
            Fac(5, "C1", 0.33m, 95);

            var f = await FilaCliente("C1");

            Assert.Equal(f.Vigente + f.De1a30 + f.De31a60 + f.De61a90 + f.Mas90, f.Total);
            Assert.Equal(1.65m, f.Total);
        }

        // ---- Orden ----

        [Fact]
        public async Task ObtenerAsync_OrdenaPorTotalDescendente_LuegoNombre_LuegoCodigo()
        {
            Socio("C1", "C", "Zeta");
            Socio("C2", "C", "alfa");
            Socio("C3", "C", "Beta");
            Socio("C4", "C", "Grande");
            Socio("C5", "C", "Beta");
            Fac(1, "C1", 100m, 10);
            Fac(2, "C2", 100m, 10);
            Fac(3, "C3", 100m, 10);
            Fac(4, "C4", 500m, 10);
            Fac(5, "C5", 100m, 10);

            var r = await _servicio.ObtenerAsync("C");

            Assert.Equal(new[] { "C4", "C2", "C3", "C5", "C1" }, r.Select(f => f.CodigoSn).ToArray());
        }

        [Fact]
        public async Task ObtenerAsync_NombreNulo_SeEntregaVacio_YSocioFaltanteTambienAparece()
        {
            _socios.Add(new SocioNegocio { Codigo = "C1", Nombre = null, TipoSn = "C" });
            Fac(1, "C1", 10m, 10);
            Fac(2, "SINSOCIO", 20m, 10);

            var r = await _servicio.ObtenerAsync("C");

            Assert.Equal(string.Empty, r.Single(f => f.CodigoSn == "C1").NombreSn);
            Assert.Equal(string.Empty, r.Single(f => f.CodigoSn == "SINSOCIO").NombreSn);
        }

        // ---- Carga agrupada ----

        [Fact]
        public async Task ObtenerAsync_Cobrar_ConsultaUnaVezCadaFuenteYSoloEnviaLasAbiertas()
        {
            Socio("C1", "C");
            Socio("C2", "C");
            Fac(1, "C1", 10m, 10);
            Fac(2, "C2", 20m, 10);
            Fac(3, "C2", 30m, 10, sub: "ND");
            Fac(4, "C2", 99m, 10, cancelado: "S");

            await _servicio.ObtenerAsync("C");

            _repoFactura.Verify(x => x.ObtenerTodoAsync(), Times.Once);
            _repoSocio.Verify(x => x.ObtenerTodoAsync(), Times.Once);
            _saldoVenta.Verify(x => x.ObtenerAsync(It.IsAny<IEnumerable<int>>()), Times.Once);
            Assert.Equal(new[] { 1, 2, 3 }, Assert.Single(_llamadasVenta).OrderBy(i => i).ToArray());
            _repoCompra.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _saldoCompra.Verify(x => x.ObtenerAsync(It.IsAny<IEnumerable<int>>()), Times.Never);
        }

        [Fact]
        public async Task ObtenerAsync_Pagar_ConsultaUnaVezCadaFuenteYSoloEnviaLasAbiertas()
        {
            Socio("P1", "P");
            Socio("P2", "P");
            Com(10, "P1", 10m, 10);
            Com(11, "P2", 20m, 10);
            Com(12, "P2", 30m, 10, sub: "DN");
            Com(13, "P2", 99m, 10, cancelado: "S");

            await _servicio.ObtenerAsync("P");

            _repoCompra.Verify(x => x.ObtenerTodoAsync(), Times.Once);
            _repoSocio.Verify(x => x.ObtenerTodoAsync(), Times.Once);
            _saldoCompra.Verify(x => x.ObtenerAsync(It.IsAny<IEnumerable<int>>()), Times.Once);
            Assert.Equal(new[] { 10, 11, 12 }, Assert.Single(_llamadasCompra).OrderBy(i => i).ToArray());
            _repoFactura.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _saldoVenta.Verify(x => x.ObtenerAsync(It.IsAny<IEnumerable<int>>()), Times.Never);
        }

        [Fact]
        public async Task ObtenerAsync_SinDocumentos_NoLlamaAlServicioDeSaldoNiConsultaSocios()
        {
            Socio("C1", "C");

            var r = await _servicio.ObtenerAsync("C");

            Assert.Empty(r);
            _saldoVenta.Verify(x => x.ObtenerAsync(It.IsAny<IEnumerable<int>>()), Times.Never);
            _repoSocio.Verify(x => x.ObtenerTodoAsync(), Times.Never);
        }

        // ---- Tipo inválido ----

        [Theory]
        [InlineData("")]
        [InlineData("X")]
        [InlineData("c")]
        [InlineData("p")]
        [InlineData("CP")]
        [InlineData(null)]
        public async Task ObtenerAsync_TipoInvalido_Lanza_YNoConsulta(string? tipo)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _servicio.ObtenerAsync(tipo!));

            Assert.Equal("El tipo debe ser C o P.", ex.Message);
            _repoFactura.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _repoCompra.Verify(x => x.ObtenerTodoAsync(), Times.Never);
        }

        [Theory]
        [InlineData("")]
        [InlineData("x")]
        [InlineData("c")]
        [InlineData(null)]
        public async Task ObtenerDetalleAsync_TipoInvalido_Lanza_YNoConsulta(string? tipo)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _servicio.ObtenerDetalleAsync(tipo!, "C1"));

            Assert.Equal("El tipo debe ser C o P.", ex.Message);
            _repoFactura.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _repoCompra.Verify(x => x.ObtenerTodoAsync(), Times.Never);
        }

        // ---- Detalle ----

        [Fact]
        public async Task ObtenerDetalleAsync_Cliente_DevuelveSusDocumentosOrdenadosPorVencimiento()
        {
            Socio("C1", "C");
            Socio("C2", "C");
            Fac(1, "C1", 100m, 10, numDoc: 501);
            Fac(2, "C1", 50m, 100, numDoc: 502);
            Fac(3, "C1", 25m, -5, sub: "ND", numDoc: 503);
            Fac(4, "C1", 0m, 20, numDoc: 504);              // sin saldo
            Fac(5, "C1", 10m, 20, cancelado: "S");           // cancelada
            Fac(6, "C2", 77m, 20);                           // otro socio

            var r = await _servicio.ObtenerDetalleAsync("C", "C1");

            Assert.Equal(new[] { 2, 1, 3 }, r.Select(d => d.Entry).ToArray());
            Assert.Equal(new[] { "Factura", "Factura", "Nota de débito" }, r.Select(d => d.Tipo).ToArray());
            Assert.Equal(new[] { 502, 501, 503 }, r.Select(d => d.NumDoc).ToArray());
            Assert.Equal(new[] { 100, 10, -5 }, r.Select(d => d.DiasVencido).ToArray());
            Assert.Equal(new[] { 50m, 100m, 25m }, r.Select(d => d.Saldo).ToArray());
            Assert.Equal(new[] { Hoy.AddDays(-100), Hoy.AddDays(-10), Hoy.AddDays(5) }, r.Select(d => d.Vencimiento).ToArray());
            Assert.Equal(Hoy.AddDays(-120), r[0].FechaDoc);
        }

        [Fact]
        public async Task ObtenerDetalleAsync_Proveedor_UsaLosTiposDeCompra()
        {
            Socio("P1", "P");
            Com(10, "P1", 40m, 5, numDoc: 701);
            Com(11, "P1", 15m, 8, sub: "DN", numDoc: 702);

            var r = await _servicio.ObtenerDetalleAsync("P", "P1");

            Assert.Equal(new[] { "Nota de débito de proveedor", "Factura de compra" }, r.Select(d => d.Tipo).ToArray());
            Assert.Equal(new[] { 702, 701 }, r.Select(d => d.NumDoc).ToArray());
        }

        [Fact]
        public async Task ObtenerDetalleAsync_MismoVencimiento_DesempataPorNumDoc()
        {
            Socio("C1", "C");
            Fac(1, "C1", 10m, 10, numDoc: 30);
            Fac(2, "C1", 10m, 10, numDoc: 10);
            Fac(3, "C1", 10m, 10, numDoc: 20);

            var r = await _servicio.ObtenerDetalleAsync("C", "C1");

            Assert.Equal(new[] { 10, 20, 30 }, r.Select(d => d.NumDoc).ToArray());
        }

        [Fact]
        public async Task ObtenerDetalleAsync_PagoParcial_MuestraElSaldoRestante()
        {
            Socio("C1", "C");
            Fac(1, "C1", 100m, 10);
            _saldosVenta[1] = 40.5m;

            var d = Assert.Single(await _servicio.ObtenerDetalleAsync("C", "C1"));

            Assert.Equal(40.5m, d.Saldo);
        }

        [Fact]
        public async Task ObtenerDetalleAsync_VencimientoNulo_UsaFechaDoc()
        {
            Socio("C1", "C");
            _facturas.Add(new Factura { Entry = 1, NumDoc = 1, CodigoSn = "C1", DocSubType = "--", Cancelado = "N", FechaDoc = Hoy.AddDays(-45).AddHours(10), FechaVencimiento = null });
            _saldosVenta[1] = 10m;

            var d = Assert.Single(await _servicio.ObtenerDetalleAsync("C", "C1"));

            Assert.Equal(Hoy.AddDays(-45), d.Vencimiento);
            Assert.Equal(45, d.DiasVencido);
            Assert.Equal(Hoy.AddDays(-45).AddHours(10), d.FechaDoc);
        }

        [Fact]
        public async Task ObtenerDetalleAsync_SinFechaDoc_UsaFechaEmision()
        {
            Socio("C1", "C");
            _facturas.Add(new Factura { Entry = 1, NumDoc = 1, CodigoSn = "C1", DocSubType = "--", Cancelado = "N", FechaDoc = null, FechaEmision = Hoy.AddDays(-12), FechaVencimiento = Hoy.AddDays(-2) });
            _saldosVenta[1] = 10m;

            var d = Assert.Single(await _servicio.ObtenerDetalleAsync("C", "C1"));

            Assert.Equal(Hoy.AddDays(-12), d.FechaDoc);
            Assert.Equal(2, d.DiasVencido);
        }

        [Fact]
        public async Task ObtenerDetalleAsync_ConsultaUnaVezY_SoloEnviaLosDocumentosDelSocio()
        {
            Socio("C1", "C");
            Socio("C2", "C");
            Fac(1, "C1", 10m, 10);
            Fac(2, "C1", 20m, 10);
            Fac(3, "C2", 30m, 10);
            Fac(4, "C1", 99m, 10, cancelado: "S");

            await _servicio.ObtenerDetalleAsync("C", "C1");

            _repoFactura.Verify(x => x.ObtenerTodoAsync(), Times.Once);
            _saldoVenta.Verify(x => x.ObtenerAsync(It.IsAny<IEnumerable<int>>()), Times.Once);
            Assert.Equal(new[] { 1, 2 }, Assert.Single(_llamadasVenta).OrderBy(i => i).ToArray());
            _repoCompra.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _saldoCompra.Verify(x => x.ObtenerAsync(It.IsAny<IEnumerable<int>>()), Times.Never);
        }

        [Fact]
        public async Task ObtenerDetalleAsync_SocioDelOtroTipo_DevuelveVacio()
        {
            Socio("P1", "P");
            Com(10, "P1", 40m, 5);

            var r = await _servicio.ObtenerDetalleAsync("C", "P1");

            Assert.Empty(r);
        }

        [Fact]
        public async Task ObtenerDetalleAsync_SocioInexistenteONulo_DevuelveVacio()
        {
            Socio("C1", "C");
            Fac(1, "C1", 10m, 10);

            Assert.Empty(await _servicio.ObtenerDetalleAsync("C", "NOEXISTE"));
            Assert.Empty(await _servicio.ObtenerDetalleAsync("C", ""));
            Assert.Empty(await _servicio.ObtenerDetalleAsync("C", null!));
        }

        [Fact]
        public async Task ObtenerDetalleAsync_SocioSinDocumentos_NoLlamaAlServicioDeSaldo()
        {
            Socio("C1", "C");

            var r = await _servicio.ObtenerDetalleAsync("C", "C1");

            Assert.Empty(r);
            _saldoVenta.Verify(x => x.ObtenerAsync(It.IsAny<IEnumerable<int>>()), Times.Never);
        }

        [Fact]
        public async Task ObtenerDetalle_CoincideConLaFilaDelSocio()
        {
            Socio("C1", "C");
            Fac(1, "C1", 10.10m, -2);
            Fac(2, "C1", 20.20m, 3);
            Fac(3, "C1", 30.30m, 50);
            Fac(4, "C1", 40.40m, 200);

            var fila = await FilaCliente("C1");
            var detalle = await _servicio.ObtenerDetalleAsync("C", "C1");

            Assert.Equal(fila.Total, detalle.Sum(d => d.Saldo));
        }
    }
}
