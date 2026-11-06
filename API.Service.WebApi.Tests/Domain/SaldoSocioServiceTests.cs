using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class SaldoSocioServiceTests
    {
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocio = new();
        private readonly Mock<IRepositorioGenerico<Factura, int>> _repoFactura = new();
        private readonly Mock<IRepositorioGenerico<FacturaCompra, int>> _repoCompra = new();
        private readonly Mock<ISaldoFacturaService> _saldoVenta = new();
        private readonly Mock<ISaldoFacturaCompraService> _saldoCompra = new();
        private readonly SaldoSocioService _servicio;

        private readonly List<SocioNegocio> _socios = new();
        private readonly List<Factura> _facturas = new();
        private readonly List<FacturaCompra> _compras = new();
        // Saldo por entry que devuelven los servicios de saldo de documento.
        private readonly Dictionary<int, decimal> _saldosVenta = new();
        private readonly Dictionary<int, decimal> _saldosCompra = new();

        private static readonly DateTime Hoy = DateTime.Today;

        public SaldoSocioServiceTests()
        {
            _servicio = new SaldoSocioService(_repoSocio.Object, _repoFactura.Object, _repoCompra.Object, _saldoVenta.Object, _saldoCompra.Object);
            _repoSocio.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _socios.AsAsyncQueryable());
            _repoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _facturas.AsAsyncQueryable());
            _repoCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _compras.AsAsyncQueryable());
            _saldoVenta.Setup(s => s.ObtenerAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync((IEnumerable<int> ids) => (IReadOnlyDictionary<int, SaldoFactura>)ids
                    .Where(_saldosVenta.ContainsKey)
                    .ToDictionary(i => i, i => new SaldoFactura(i, _saldosVenta[i], 0m, 0m, _saldosVenta[i], "Pendiente")));
            _saldoCompra.Setup(s => s.ObtenerAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync((IEnumerable<int> ids) => (IReadOnlyDictionary<int, SaldoFacturaCompra>)ids
                    .Where(_saldosCompra.ContainsKey)
                    .ToDictionary(i => i, i => new SaldoFacturaCompra(i, _saldosCompra[i], 0m, 0m, _saldosCompra[i], "Pendiente")));
        }

        private void Socio(string codigo, string tipo) => _socios.Add(new SocioNegocio { Codigo = codigo, Nombre = codigo, TipoSn = tipo });

        private void Fac(int entry, string codigo, decimal saldo, DateTime? vence, DateTime? fechaDoc = null, string cancelado = "N", string sub = "--")
        {
            _facturas.Add(new Factura { Entry = entry, CodigoSn = codigo, DocSubType = sub, Cancelado = cancelado, FechaDoc = fechaDoc ?? Hoy.AddDays(-60), FechaVencimiento = vence });
            _saldosVenta[entry] = saldo;
        }

        private void Com(int entry, string codigo, decimal saldo, DateTime? vence, DateTime? fechaDoc = null, string cancelado = "N", string sub = "--")
        {
            _compras.Add(new FacturaCompra { Entry = entry, CodigoSn = codigo, DocSubType = sub, Cancelado = cancelado, FechaDoc = fechaDoc ?? Hoy.AddDays(-60), FechaVencimiento = vence });
            _saldosCompra[entry] = saldo;
        }

        [Fact]
        public async Task ObtenerAsync_Cliente_SumaSaldoYVencido()
        {
            Socio("C1", "C");
            Fac(1, "C1", 100m, Hoy.AddDays(-1));   // vencida
            Fac(2, "C1", 50m, Hoy.AddDays(10));    // vigente
            Fac(3, "C1", 25.5m, Hoy.AddDays(-30)); // vencida

            var r = await _servicio.ObtenerAsync(new[] { "C1" });

            var s = r["C1"];
            Assert.Equal("C", s.Tipo);
            Assert.Equal(175.5m, s.Saldo);
            Assert.Equal(125.5m, s.Vencido);
            Assert.Equal(3, s.DocumentosAbiertos);
        }

        [Fact]
        public async Task ObtenerAsync_ClienteYProveedorEnLaMismaLlamada()
        {
            Socio("C1", "C");
            Socio("P1", "P");
            Fac(1, "C1", 100m, Hoy.AddDays(-5));
            Com(10, "P1", 70m, Hoy.AddDays(5));

            var r = await _servicio.ObtenerAsync(new[] { "C1", "P1" });

            Assert.Equal(2, r.Count);
            Assert.Equal(new SaldoSocio("C1", "C", 100m, 100m, 1), r["C1"]);
            Assert.Equal(new SaldoSocio("P1", "P", 70m, 0m, 1), r["P1"]);
        }

        [Fact]
        public async Task ObtenerAsync_VariosSocios_ConsultaDocumentosYSaldosUnaVez()
        {
            Socio("C1", "C");
            Socio("C2", "C");
            Socio("P1", "P");
            Socio("P2", "P");
            Fac(1, "C1", 10m, Hoy);
            Fac(2, "C2", 20m, Hoy.AddDays(-2));
            Com(10, "P1", 30m, Hoy);
            Com(11, "P2", 40m, Hoy.AddDays(-2));

            var r = await _servicio.ObtenerAsync(new[] { "C1", "C2", "P1", "P2" });

            Assert.Equal(10m, r["C1"].Saldo);
            Assert.Equal(20m, r["C2"].Saldo);
            Assert.Equal(30m, r["P1"].Saldo);
            Assert.Equal(40m, r["P2"].Saldo);
            _repoSocio.Verify(x => x.ObtenerTodoAsync(), Times.Once);
            _repoFactura.Verify(x => x.ObtenerTodoAsync(), Times.Once);
            _repoCompra.Verify(x => x.ObtenerTodoAsync(), Times.Once);
            _saldoVenta.Verify(x => x.ObtenerAsync(It.IsAny<IEnumerable<int>>()), Times.Once);
            _saldoCompra.Verify(x => x.ObtenerAsync(It.IsAny<IEnumerable<int>>()), Times.Once);
        }

        [Fact]
        public async Task ObtenerAsync_SoloClientes_NoConsultaCompras()
        {
            Socio("C1", "C");
            Fac(1, "C1", 10m, Hoy);

            await _servicio.ObtenerAsync(new[] { "C1" });

            _repoCompra.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _saldoCompra.Verify(x => x.ObtenerAsync(It.IsAny<IEnumerable<int>>()), Times.Never);
        }

        [Fact]
        public async Task ObtenerAsync_DocumentoSaldadoNoCuentaNiVence()
        {
            Socio("C1", "C");
            Fac(1, "C1", 0m, Hoy.AddDays(-40));    // pagada
            Fac(2, "C1", 80m, Hoy.AddDays(-40));   // abierta y vencida

            var s = (await _servicio.ObtenerAsync(new[] { "C1" }))["C1"];

            Assert.Equal(80m, s.Saldo);
            Assert.Equal(80m, s.Vencido);
            Assert.Equal(1, s.DocumentosAbiertos);
        }

        [Fact]
        public async Task ObtenerAsync_DocumentoCancelado_QuedaFuera()
        {
            Socio("C1", "C");
            Socio("P1", "P");
            Fac(1, "C1", 100m, Hoy.AddDays(-10), cancelado: "S");
            Com(10, "P1", 100m, Hoy.AddDays(-10), cancelado: "S");

            var r = await _servicio.ObtenerAsync(new[] { "C1", "P1" });

            Assert.Equal(0m, r["C1"].Saldo);
            Assert.Equal(0, r["C1"].DocumentosAbiertos);
            Assert.Equal(0m, r["P1"].Saldo);
            Assert.Equal(0, r["P1"].DocumentosAbiertos);
        }

        [Fact]
        public async Task ObtenerAsync_VencimientoNulo_UsaFechaDoc()
        {
            Socio("C1", "C");
            Socio("P1", "P");
            Fac(1, "C1", 60m, null, fechaDoc: Hoy.AddDays(-3));   // vencida por FechaDoc
            Fac(2, "C1", 40m, null, fechaDoc: Hoy);               // vence hoy: aun no vencida
            Com(10, "P1", 15m, null, fechaDoc: Hoy.AddDays(-1));

            var r = await _servicio.ObtenerAsync(new[] { "C1", "P1" });

            Assert.Equal(100m, r["C1"].Saldo);
            Assert.Equal(60m, r["C1"].Vencido);
            Assert.Equal(15m, r["P1"].Vencido);
        }

        [Fact]
        public async Task ObtenerAsync_VenceHoy_NoEsVencido()
        {
            Socio("C1", "C");
            Fac(1, "C1", 100m, Hoy.AddHours(15));

            var s = (await _servicio.ObtenerAsync(new[] { "C1" }))["C1"];

            Assert.Equal(100m, s.Saldo);
            Assert.Equal(0m, s.Vencido);
        }

        [Fact]
        public async Task ObtenerAsync_NotaDebito_CuentaComoDocumento()
        {
            Socio("C1", "C");
            Socio("P1", "P");
            Fac(1, "C1", 30m, Hoy.AddDays(-1), sub: "ND");
            Com(10, "P1", 45m, Hoy.AddDays(-1), sub: "DN");

            var r = await _servicio.ObtenerAsync(new[] { "C1", "P1" });

            Assert.Equal(30m, r["C1"].Saldo);
            Assert.Equal(1, r["C1"].DocumentosAbiertos);
            Assert.Equal(45m, r["P1"].Saldo);
            Assert.Equal(1, r["P1"].DocumentosAbiertos);
        }

        [Fact]
        public async Task ObtenerAsync_SubtipoDistintoDeFacturaONotaDebito_NoCuenta()
        {
            Socio("C1", "C");
            Fac(1, "C1", 30m, Hoy.AddDays(-1), sub: "NC");

            var s = (await _servicio.ObtenerAsync(new[] { "C1" }))["C1"];

            Assert.Equal(0m, s.Saldo);
            Assert.Equal(0, s.DocumentosAbiertos);
        }

        [Fact]
        public async Task ObtenerAsync_SocioSinDocumentos_AparecConCeros()
        {
            Socio("C1", "C");
            Socio("P1", "P");

            var r = await _servicio.ObtenerAsync(new[] { "C1", "P1" });

            Assert.Equal(new SaldoSocio("C1", "C", 0m, 0m, 0), r["C1"]);
            Assert.Equal(new SaldoSocio("P1", "P", 0m, 0m, 0), r["P1"]);
        }

        [Fact]
        public async Task ObtenerAsync_DocumentosDeOtroSocio_NoSeMezclan()
        {
            Socio("C1", "C");
            Socio("C2", "C");
            Fac(1, "C1", 100m, Hoy.AddDays(-1));
            Fac(2, "C2", 7m, Hoy.AddDays(-1));

            var r = await _servicio.ObtenerAsync(new[] { "C1" });

            Assert.Single(r);
            Assert.Equal(100m, r["C1"].Saldo);
        }

        [Fact]
        public async Task ObtenerAsync_SocioInexistente_NoAparece()
        {
            Socio("C1", "C");

            var r = await _servicio.ObtenerAsync(new[] { "C1", "NOEXISTE" });

            Assert.True(r.ContainsKey("C1"));
            Assert.False(r.ContainsKey("NOEXISTE"));
        }

        [Fact]
        public async Task ObtenerAsync_EntradaVaciaONula_NoConsulta()
        {
            var vacio = await _servicio.ObtenerAsync(Array.Empty<string>());
            var nulo = await _servicio.ObtenerAsync(null!);

            Assert.Empty(vacio);
            Assert.Empty(nulo);
            _repoSocio.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _repoFactura.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _repoCompra.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _saldoVenta.Verify(x => x.ObtenerAsync(It.IsAny<IEnumerable<int>>()), Times.Never);
            _saldoCompra.Verify(x => x.ObtenerAsync(It.IsAny<IEnumerable<int>>()), Times.Never);
        }

        [Fact]
        public async Task ObtenerAsync_CodigosRepetidos_SeAtiendenUnaVez()
        {
            Socio("C1", "C");
            Fac(1, "C1", 100m, Hoy.AddDays(-1));

            var r = await _servicio.ObtenerAsync(new[] { "C1", "C1" });

            Assert.Single(r);
            Assert.Equal(100m, r["C1"].Saldo);
            Assert.Equal(1, r["C1"].DocumentosAbiertos);
        }

        [Fact]
        public async Task ObtenerAsync_CodigoDeDocumentoConOtraCapitalizacion_SigueSiendoDelSocio()
        {
            // La base compara sin distinguir mayúsculas: el documento 'cl001' pertenece al socio 'CL001'.
            _repoSocio.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _socios.AsAsyncQueryableSinMayusculas());
            _repoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _facturas.AsAsyncQueryableSinMayusculas());
            Socio("CL001", "C");
            Fac(1, "cl001", 100m, Hoy.AddDays(-1));
            Fac(2, "CL001", 50m, Hoy.AddDays(10));

            var r = await _servicio.ObtenerAsync(new[] { "cl001" });

            var s = Assert.Single(r).Value;
            Assert.Equal("CL001", s.CodigoSn);
            Assert.Equal(150m, s.Saldo);
            Assert.Equal(100m, s.Vencido);
            Assert.Equal(2, s.DocumentosAbiertos);
            Assert.Equal(150m, r["CL001"].Saldo);
            Assert.Equal(150m, r["cl001"].Saldo);
        }

        [Fact]
        public async Task ObtenerAsync_RedondeaADosDecimales()
        {
            Socio("C1", "C");
            Fac(1, "C1", 0.005m, Hoy.AddDays(-1));
            Fac(2, "C1", 0.004m, Hoy.AddDays(-1));

            var s = (await _servicio.ObtenerAsync(new[] { "C1" }))["C1"];

            Assert.Equal(0.01m, s.Saldo);
            Assert.Equal(0.01m, s.Vencido);
        }
    }
}
