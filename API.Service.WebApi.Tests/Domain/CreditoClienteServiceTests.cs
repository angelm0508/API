using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class CreditoClienteServiceTests
    {
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocio = new();
        private readonly Mock<IRepositorioGenerico<Pedido, int>> _repoPedido = new();
        private readonly Mock<IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)>> _repoPedidoDet = new();
        private readonly Mock<IRepositorioGenerico<Entrega, int>> _repoEntrega = new();
        private readonly Mock<IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)>> _repoEntregaDet = new();
        private readonly Mock<IRepositorioGenerico<Factura, int>> _repoFactura = new();
        private readonly Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>> _repoFacturaDet = new();
        private readonly Mock<ISaldoSocioService> _saldoSocio = new();
        private readonly CreditoClienteService _servicio;

        private readonly List<SocioNegocio> _socios = new();
        private readonly List<Pedido> _pedidos = new();
        private readonly List<PedidoDetalle> _pedidoLineas = new();
        private readonly List<Entrega> _entregas = new();
        private readonly List<EntregaDetalle> _entregaLineas = new();
        private readonly List<Factura> _facturas = new();
        private readonly List<FacturaDetalle> _facturaLineas = new();
        // Saldo y vencido que devuelve ISaldoSocioService por socio.
        private readonly Dictionary<string, (decimal Saldo, decimal Vencido)> _saldos = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<List<string>> _llamadasSaldo = new();

        public CreditoClienteServiceTests()
        {
            _servicio = new CreditoClienteService(_repoSocio.Object, _repoPedido.Object, _repoPedidoDet.Object,
                _repoEntrega.Object, _repoEntregaDet.Object, _repoFactura.Object, _repoFacturaDet.Object, _saldoSocio.Object);
            _repoSocio.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _socios.AsAsyncQueryableSinMayusculas());
            _repoPedido.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _pedidos.AsAsyncQueryableSinMayusculas());
            _repoPedidoDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _pedidoLineas.AsAsyncQueryable());
            _repoEntrega.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _entregas.AsAsyncQueryableSinMayusculas());
            _repoEntregaDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _entregaLineas.AsAsyncQueryable());
            _repoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _facturas.AsAsyncQueryableSinMayusculas());
            _repoFacturaDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _facturaLineas.AsAsyncQueryable());
            _saldoSocio.Setup(s => s.ObtenerAsync(It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync((IEnumerable<string> codigos) =>
                {
                    var lista = codigos.ToList();
                    _llamadasSaldo.Add(lista);
                    return (IReadOnlyDictionary<string, SaldoSocio>)lista
                        .Where(_saldos.ContainsKey)
                        .ToDictionary(c => c, c => new SaldoSocio(c, "C", _saldos[c].Saldo, _saldos[c].Vencido, 0), StringComparer.OrdinalIgnoreCase);
                });
        }

        // ---- Datos de prueba ----

        private void Socio(string codigo, string tipo = "C", decimal? limite = null, string bloquear = "N") =>
            _socios.Add(new SocioNegocio { Codigo = codigo, Nombre = codigo, TipoSn = tipo, LimiteCredito = limite, BloquearSiVencido = bloquear });

        private void Saldo(string codigo, decimal saldo, decimal vencido) => _saldos[codigo] = (saldo, vencido);

        private void Ped(int entry, string codigo, decimal totalDoc, string cancelado = "N", string estado = "O") =>
            _pedidos.Add(new Pedido { Entry = entry, CodigoSn = codigo, TotalDoc = totalDoc, Cancelado = cancelado, EstadoDoc = estado });

        private void PedLin(int entry, int linea, decimal cantidad, decimal totalLinea) =>
            _pedidoLineas.Add(new PedidoDetalle { Entry = entry, NoLinea = linea, Cantidad = cantidad, TotalLinea = totalLinea });

        private void Ent(int entry, string codigo, decimal totalDoc, string cancelado = "N", string estado = "O") =>
            _entregas.Add(new Entrega { Entry = entry, CodigoSn = codigo, TotalDoc = totalDoc, Cancelado = cancelado, EstadoDoc = estado });

        private void EntLin(int entry, int linea, decimal cantidad, decimal totalLinea, int? baseTipo = null, int? baseEntry = null, int? baseLinea = null) =>
            _entregaLineas.Add(new EntregaDetalle { Entry = entry, NoLinea = linea, Cantidad = cantidad, TotalLinea = totalLinea, BaseTipo = baseTipo, BaseEntry = baseEntry, BaseLinea = baseLinea });

        private void Fac(int entry, string codigo, string cancelado = "N", string estado = "O", string sub = "--") =>
            _facturas.Add(new Factura { Entry = entry, CodigoSn = codigo, Cancelado = cancelado, EstadoDoc = estado, DocSubType = sub });

        private void FacLin(int entry, int linea, decimal cantidad, int baseTipo, int baseEntry, int baseLinea) =>
            _facturaLineas.Add(new FacturaDetalle { Entry = entry, NoLinea = linea, Cantidad = cantidad, BaseTipo = baseTipo, BaseEntry = baseEntry, BaseLinea = baseLinea });

        // Pedido P1 (entry 1) del enunciado: TotalDoc 1100, L1 (10 u, 600) y L2 (5 u, 400) -> factor 1.1.
        private void PedidoP1(string codigo = "C1")
        {
            Ped(1, codigo, 1100m);
            PedLin(1, 1, 10m, 600m);
            PedLin(1, 2, 5m, 400m);
        }

        // Entrega E1 (entry 10) copia 4 unidades de L1 de P1: TotalDoc 264, una linea de 4 u por 240.
        private void EntregaE1(string codigo = "C1", string cancelado = "N", string estado = "O")
        {
            Ent(10, codigo, 264m, cancelado, estado);
            EntLin(10, 1, 4m, 240m, baseTipo: 4, baseEntry: 1, baseLinea: 1);
        }

        // ---- 1. Sin documentos ----

        [Fact]
        public async Task ObtenerPosicion_SinDocumentos_ComprometidoCeroYDisponibleRestaLaExposicion()
        {
            Socio("C1", limite: 1000m);
            Saldo("C1", 300m, 50m);

            var p = await _servicio.ObtenerPosicionAsync("C1");

            Assert.NotNull(p);
            Assert.Equal("C1", p!.CodigoSn);
            Assert.Equal(1000m, p.Limite);
            Assert.Equal(300m, p.Saldo);
            Assert.Equal(50m, p.Vencido);
            Assert.Equal(0m, p.Comprometido);
            Assert.Equal(300m, p.Exposicion);
            Assert.Equal(700m, p.Disponible);
        }

        [Fact]
        public async Task ObtenerPosicion_ExposicionMayorQueLimite_DisponibleNegativo()
        {
            Socio("C1", limite: 500m);
            Saldo("C1", 300m, 0m);
            PedidoP1();

            var p = await _servicio.ObtenerPosicionAsync("C1");

            Assert.Equal(1400m, p!.Exposicion);
            Assert.Equal(-900m, p.Disponible);
        }

        // ---- 2. Pedido con descuento/impuesto (factor) ----

        [Fact]
        public async Task ObtenerPosicion_PedidoAbierto_ValorConFactorDeCabecera()
        {
            Socio("C1", limite: 5000m);
            PedidoP1();

            var p = await _servicio.ObtenerPosicionAsync("C1");

            Assert.Equal(1100m, p!.Comprometido);
            Assert.Equal(1100m, p.Exposicion);
            Assert.Equal(3900m, p.Disponible);
        }

        // ---- 3. Copiar parcial no cambia el total ----

        [Fact]
        public async Task ObtenerPosicion_EntregaActivaCopiaParteDelPedido_ElTotalNoCambia()
        {
            Socio("C1");
            PedidoP1();
            EntregaE1();

            var p = await _servicio.ObtenerPosicionAsync("C1");

            // P1 abierto = 6/10 x 600 x 1.1 + 440 = 836; E1 = 264.
            Assert.Equal(1100m, p!.Comprometido);
            Assert.Equal(836m, await _servicio.ValorAbiertoDocumentoAsync(4, 1));
            Assert.Equal(264m, await _servicio.ValorAbiertoDocumentoAsync(5, 10));
        }

        // ---- 4. Entrega cancelada ----

        [Theory]
        [InlineData("S", "O")]
        [InlineData("N", "C")]
        public async Task ObtenerPosicion_EntregaCancelada_NoCuentaYElPedidoVuelveCompleto(string cancelado, string estado)
        {
            Socio("C1");
            PedidoP1();
            EntregaE1(cancelado: cancelado, estado: estado);

            var p = await _servicio.ObtenerPosicionAsync("C1");

            Assert.Equal(1100m, p!.Comprometido);
            Assert.Equal(1100m, await _servicio.ValorAbiertoDocumentoAsync(4, 1));
        }

        // ---- 5. Factura consume la entrega ----

        [Fact]
        public async Task ObtenerPosicion_FacturaActivaCopiaElRestoDeLaEntrega_LaEntregaYaNoCompromete()
        {
            Socio("C1");
            Ent(10, "C1", 264m);
            EntLin(10, 1, 4m, 240m);
            Fac(20, "C1");
            FacLin(20, 1, 4m, baseTipo: 5, baseEntry: 10, baseLinea: 1);

            var p = await _servicio.ObtenerPosicionAsync("C1");

            Assert.Equal(0m, p!.Comprometido);
        }

        [Fact]
        public async Task ObtenerPosicion_FacturaConsumeLaEntregaPeroElPedidoSigueAbierto()
        {
            Socio("C1");
            PedidoP1();
            EntregaE1();
            Fac(20, "C1");
            FacLin(20, 1, 4m, baseTipo: 5, baseEntry: 10, baseLinea: 1);

            var p = await _servicio.ObtenerPosicionAsync("C1");

            // E1 queda en 0 (paso a saldo); P1 abierto sigue siendo 836.
            Assert.Equal(836m, p!.Comprometido);
        }

        [Fact]
        public async Task ObtenerPosicion_FacturaActivaCopiaPedidoDirecto_ConsumeSuLinea()
        {
            Socio("C1");
            PedidoP1();
            Fac(20, "C1");
            FacLin(20, 1, 10m, baseTipo: 4, baseEntry: 1, baseLinea: 1);

            var p = await _servicio.ObtenerPosicionAsync("C1");

            Assert.Equal(440m, p!.Comprometido); // solo queda L2: 5/5 x 400 x 1.1
        }

        [Theory]
        [InlineData("S", "O")]
        [InlineData("N", "C")]
        public async Task ObtenerPosicion_FacturaCanceladaNoConsume(string cancelado, string estado)
        {
            Socio("C1");
            Ent(10, "C1", 264m);
            EntLin(10, 1, 4m, 240m);
            Fac(20, "C1", cancelado, estado);
            FacLin(20, 1, 4m, baseTipo: 5, baseEntry: 10, baseLinea: 1);

            var p = await _servicio.ObtenerPosicionAsync("C1");

            Assert.Equal(264m, p!.Comprometido);
        }

        [Fact]
        public async Task ObtenerPosicion_NotaDeDebitoActivaTambienConsume()
        {
            Socio("C1");
            Ent(10, "C1", 264m);
            EntLin(10, 1, 4m, 240m);
            Fac(20, "C1", sub: "ND");
            FacLin(20, 1, 4m, baseTipo: 5, baseEntry: 10, baseLinea: 1);

            var p = await _servicio.ObtenerPosicionAsync("C1");

            Assert.Equal(0m, p!.Comprometido);
        }

        [Fact]
        public async Task ObtenerPosicion_LineaDeFacturaConOtraBase_NoConsume()
        {
            Socio("C1");
            Ent(10, "C1", 264m);
            EntLin(10, 1, 4m, 240m);
            Fac(20, "C1");
            // Base 3 (cotizacion) con el mismo numero de entry: no consume la entrega 10.
            FacLin(20, 1, 4m, baseTipo: 3, baseEntry: 10, baseLinea: 1);

            var p = await _servicio.ObtenerPosicionAsync("C1");

            Assert.Equal(264m, p!.Comprometido);
        }

        // ---- 6. Documentos que no cuentan ----

        [Fact]
        public async Task ObtenerPosicion_PedidoCanceladoYEntregaConEstadoCerrado_NoCuentan()
        {
            // Cotizaciones y ordenes de servicio no cuentan: el servicio ni siquiera tiene sus repositorios.
            Socio("C1");
            Ped(1, "C1", 1000m, cancelado: "S");
            PedLin(1, 1, 1m, 1000m);
            Ped(2, "C1", 500m, estado: "C");
            PedLin(2, 1, 1m, 500m);
            Ent(10, "C1", 700m, cancelado: "S");
            EntLin(10, 1, 1m, 700m);
            Ent(11, "C1", 300m, estado: "C");
            EntLin(11, 1, 1m, 300m);

            var p = await _servicio.ObtenerPosicionAsync("C1");

            Assert.Equal(0m, p!.Comprometido);
        }

        [Fact]
        public async Task ObtenerPosicion_DocumentosDeOtroCliente_NoCuentan()
        {
            Socio("C1");
            Socio("C2");
            Ped(1, "C2", 1000m);
            PedLin(1, 1, 1m, 1000m);

            var p = await _servicio.ObtenerPosicionAsync("C1");

            Assert.Equal(0m, p!.Comprometido);
        }

        // ---- 7. Cantidad 0 y suma de lineas 0 ----

        [Fact]
        public async Task ObtenerPosicion_LineaConCantidadCero_NoCuentaNiDivideEntreCero()
        {
            Socio("C1");
            Ped(1, "C1", 220m);
            PedLin(1, 1, 0m, 100m);
            PedLin(1, 2, 1m, 100m);

            var p = await _servicio.ObtenerPosicionAsync("C1");

            // factor = 220 / 200 = 1.1; solo la segunda linea cuenta.
            Assert.Equal(110m, p!.Comprometido);
        }

        [Fact]
        public async Task ObtenerPosicion_SumaDeLineasCero_FactorUnoSinDividirEntreCero()
        {
            Socio("C1");
            Ped(1, "C1", 50m);
            PedLin(1, 1, 2m, 0m);
            Ped(2, "C1", 80m);
            PedLin(2, 1, 0m, 0m);

            var p = await _servicio.ObtenerPosicionAsync("C1");

            Assert.Equal(0m, p!.Comprometido);
        }

        [Fact]
        public async Task ObtenerPosicion_PedidoSinLineas_NoCompromete()
        {
            Socio("C1");
            Ped(1, "C1", 500m);

            var p = await _servicio.ObtenerPosicionAsync("C1");

            Assert.Equal(0m, p!.Comprometido);
        }

        // ---- 8. Redondeo ----

        [Theory]
        [InlineData(0.005, 0.01)]
        [InlineData(1.005, 1.01)]
        [InlineData(2.004, 2.00)]
        public async Task ObtenerPosicion_RedondeaDosDecimalesAwayFromZero(double valor, double esperado)
        {
            Socio("C1");
            Ped(1, "C1", (decimal)valor);
            PedLin(1, 1, 1m, (decimal)valor);

            var p = await _servicio.ObtenerPosicionAsync("C1");

            Assert.Equal((decimal)esperado, p!.Comprometido);
        }

        [Fact]
        public async Task ObtenerPosicion_RedondeaPorDocumentoYLuegoSuma()
        {
            Socio("C1");
            // Cada pedido vale 0.005 -> 0.01; la suma es 0.02 (no 0.01 de redondear el total).
            Ped(1, "C1", 0.005m);
            PedLin(1, 1, 1m, 0.005m);
            Ped(2, "C1", 0.005m);
            PedLin(2, 1, 1m, 0.005m);

            var p = await _servicio.ObtenerPosicionAsync("C1");

            Assert.Equal(0.02m, p!.Comprometido);
        }

        // ---- 9. Limite y bloqueo ----

        [Fact]
        public async Task ObtenerPosicion_SinLimite_DisponibleNull()
        {
            Socio("C1", limite: null);
            Saldo("C1", 100m, 0m);

            var p = await _servicio.ObtenerPosicionAsync("C1");

            Assert.Null(p!.Limite);
            Assert.Null(p.Disponible);
            Assert.Equal(100m, p.Exposicion);
        }

        [Fact]
        public async Task ObtenerPosicion_LimiteCero_DisponibleEsMenosLaExposicion()
        {
            Socio("C1", limite: 0m);
            Saldo("C1", 40m, 0m);

            var p = await _servicio.ObtenerPosicionAsync("C1");

            Assert.Equal(0m, p!.Limite);
            Assert.Equal(-40m, p.Disponible);
        }

        [Theory]
        [InlineData("S", true)]
        [InlineData("N", false)]
        public async Task ObtenerPosicion_BloquearSiVencido(string valor, bool esperado)
        {
            Socio("C1", bloquear: valor);

            var p = await _servicio.ObtenerPosicionAsync("C1");

            Assert.Equal(esperado, p!.BloquearSiVencido);
        }

        // ---- 10. Socios que no son cliente ----

        [Fact]
        public async Task ObtenerPosicion_ProveedorOInexistente_Null()
        {
            Socio("P1", tipo: "P");
            Saldo("P1", 500m, 0m);

            Assert.Null(await _servicio.ObtenerPosicionAsync("P1"));
            Assert.Null(await _servicio.ObtenerPosicionAsync("NOEXISTE"));
            Assert.Null(await _servicio.ObtenerPosicionAsync(""));
            _saldoSocio.Verify(s => s.ObtenerAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
        }

        [Fact]
        public async Task ObtenerPosiciones_ProveedorNoApareceYElClienteSi()
        {
            Socio("C1");
            Socio("P1", tipo: "P");

            var r = await _servicio.ObtenerPosicionesAsync(new[] { "C1", "P1", "NOEXISTE" });

            Assert.Single(r);
            Assert.True(r.ContainsKey("C1"));
        }

        // ---- 11. Carga agrupada ----

        [Fact]
        public async Task ObtenerPosiciones_TresClientes_UnaLlamadaDeSaldoYUnaConsultaPorTabla()
        {
            Socio("C1", limite: 2000m);
            Socio("C2", limite: 0m);
            Socio("C3");
            Saldo("C1", 100m, 10m);
            Saldo("C2", 200m, 0m);
            Saldo("C3", 0m, 0m);

            // C1: P1 con entrega parcial y factura que consume parte de la entrega.
            PedidoP1("C1");
            EntregaE1("C1");
            Fac(20, "C1");
            FacLin(20, 1, 1m, baseTipo: 5, baseEntry: 10, baseLinea: 1);
            // C2: un pedido; C3: una entrega.
            Ped(2, "C2", 300m);
            PedLin(2, 1, 3m, 300m);
            Ent(11, "C3", 90m);
            EntLin(11, 1, 9m, 90m);

            var r = await _servicio.ObtenerPosicionesAsync(new[] { "C1", "c1", "C2", "C3", "C2" });

            Assert.Equal(3, r.Count);
            // C1: P1 abierto 836 + E1 (4 u, consumio 1) 3/4 x 240 x 1.1 = 198.
            Assert.Equal(1034m, r["C1"].Comprometido);
            Assert.Equal(300m, r["C2"].Comprometido);
            Assert.Equal(90m, r["C3"].Comprometido);
            Assert.Equal(1134m, r["C1"].Exposicion);
            Assert.Equal(500m, r["C2"].Exposicion);
            Assert.Equal(-500m, r["C2"].Disponible);

            Assert.Single(_llamadasSaldo);
            Assert.Equal(new[] { "C1", "C2", "C3" }, _llamadasSaldo[0].OrderBy(c => c));
            _repoSocio.Verify(x => x.ObtenerTodoAsync(), Times.Once);
            _repoPedido.Verify(x => x.ObtenerTodoAsync(), Times.Once);
            _repoPedidoDet.Verify(x => x.ObtenerTodoAsync(), Times.Once);
            _repoEntrega.Verify(x => x.ObtenerTodoAsync(), Times.Once);
            _repoEntregaDet.Verify(x => x.ObtenerTodoAsync(), Times.Once);
            _repoFactura.Verify(x => x.ObtenerTodoAsync(), Times.Once);
            _repoFacturaDet.Verify(x => x.ObtenerTodoAsync(), Times.Once);
        }

        [Fact]
        public async Task ObtenerPosiciones_LaCapitalizacionDelCodigoNoDuplicaNiPierdeResultados()
        {
            Socio("C1");
            Saldo("C1", 10m, 0m);

            var r = await _servicio.ObtenerPosicionesAsync(new[] { "c1", "C1" });

            Assert.Single(r);
            Assert.Equal(10m, r["C1"].Saldo);
            Assert.Equal(10m, r["c1"].Saldo); // el diccionario no distingue mayusculas
            Assert.Equal("C1", r["C1"].CodigoSn);
        }

        [Fact]
        public async Task ObtenerPosiciones_EntradaVaciaONula_DevuelveVacioSinConsultar()
        {
            var vacio = await _servicio.ObtenerPosicionesAsync(Array.Empty<string>());
            var soloVacios = await _servicio.ObtenerPosicionesAsync(new[] { "", null! });
            var nulo = await _servicio.ObtenerPosicionesAsync(null!);

            Assert.Empty(vacio);
            Assert.Empty(soloVacios);
            Assert.Empty(nulo);
            _repoSocio.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _repoPedido.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _repoEntrega.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _saldoSocio.Verify(s => s.ObtenerAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
        }

        [Fact]
        public async Task ObtenerPosiciones_ClienteSinPedidosNiEntregas_NoConsultaLineasNiFacturas()
        {
            Socio("C1");

            await _servicio.ObtenerPosicionesAsync(new[] { "C1" });

            _repoPedidoDet.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _repoEntregaDet.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _repoFactura.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _repoFacturaDet.Verify(x => x.ObtenerTodoAsync(), Times.Never);
        }

        // ---- 12. Valor abierto de un documento ----

        [Fact]
        public async Task ValorAbierto_PedidoCancelado_DevuelveElValorCompletoIgualmente()
        {
            Ped(1, "C1", 1100m, cancelado: "S");
            PedLin(1, 1, 10m, 600m);
            PedLin(1, 2, 5m, 400m);

            Assert.Equal(1100m, await _servicio.ValorAbiertoDocumentoAsync(4, 1));
        }

        [Fact]
        public async Task ValorAbierto_EntryInexistenteOTipoNoSoportado_Cero()
        {
            PedidoP1();

            Assert.Equal(0m, await _servicio.ValorAbiertoDocumentoAsync(4, 999));
            Assert.Equal(0m, await _servicio.ValorAbiertoDocumentoAsync(5, 999));
            Assert.Equal(0m, await _servicio.ValorAbiertoDocumentoAsync(3, 1));
            Assert.Equal(0m, await _servicio.ValorAbiertoDocumentoAsync(70, 1));
        }

        [Fact]
        public async Task ValorAbierto_ConEntregaActivaQueConsumeParte_DescuentaLoConsumido()
        {
            PedidoP1();
            EntregaE1();

            Assert.Equal(836m, await _servicio.ValorAbiertoDocumentoAsync(4, 1));
        }

        [Fact]
        public async Task ValorAbierto_ConEntregaCancelada_NoDescuenta()
        {
            PedidoP1();
            EntregaE1(cancelado: "S");

            Assert.Equal(1100m, await _servicio.ValorAbiertoDocumentoAsync(4, 1));
        }

        [Fact]
        public async Task ValorAbierto_ConsumoDeUnClienteDistinto_TambienDescuenta()
        {
            // La entrega consumidora no es del cliente del pedido: su estado se resuelve igualmente.
            PedidoP1("C1");
            EntregaE1("C9");

            Assert.Equal(836m, await _servicio.ValorAbiertoDocumentoAsync(4, 1));
        }

        [Fact]
        public async Task ValorAbierto_Entrega_ConsumidaPorFacturaActiva()
        {
            Ent(10, "C1", 264m);
            EntLin(10, 1, 4m, 240m);
            Fac(20, "C1");
            FacLin(20, 1, 1m, baseTipo: 5, baseEntry: 10, baseLinea: 1);

            // 3/4 x 240 x 1.1
            Assert.Equal(198m, await _servicio.ValorAbiertoDocumentoAsync(5, 10));
        }

        [Fact]
        public async Task ValorAbierto_EntregaCancelada_DevuelveSuValor()
        {
            Ent(10, "C1", 264m, cancelado: "S");
            EntLin(10, 1, 4m, 240m);

            Assert.Equal(264m, await _servicio.ValorAbiertoDocumentoAsync(5, 10));
        }

        [Fact]
        public async Task ValorAbierto_ConsumoSobreExcedido_NoDaValorNegativo()
        {
            Ped(1, "C1", 100m);
            PedLin(1, 1, 2m, 100m);
            Ent(10, "C1", 100m);
            EntLin(10, 1, 3m, 100m, baseTipo: 4, baseEntry: 1, baseLinea: 1);

            Assert.Equal(0m, await _servicio.ValorAbiertoDocumentoAsync(4, 1));
        }

        // ---- 13. Verificacion de un documento nuevo ----

        [Fact]
        public async Task Verificar_SinLimite_Permitido()
        {
            Socio("C1", limite: null);
            Saldo("C1", 999999m, 0m);

            var r = await _servicio.VerificarAsync("C1", 5000m);

            Assert.True(r.Permitido);
            Assert.Null(r.Motivo);
            Assert.Equal(0m, r.Exceso);
            Assert.Equal(5000m, r.Monto);
            Assert.NotNull(r.Posicion);
        }

        [Fact]
        public async Task Verificar_ExposicionFinalIgualAlLimite_Permitido()
        {
            Socio("C1", limite: 1000m);
            Saldo("C1", 300m, 0m);
            Ped(1, "C1", 500m);
            PedLin(1, 1, 1m, 500m);

            var r = await _servicio.VerificarAsync("C1", 200m);

            Assert.True(r.Permitido); // 300 + 500 + 200 = 1000: no es mayor que el limite
            Assert.Null(r.Motivo);
            Assert.Equal(0m, r.Exceso);
            Assert.Equal(800m, r.Posicion!.Exposicion);
        }

        [Fact]
        public async Task Verificar_ExposicionFinalSuperaElLimite_LimiteConExceso()
        {
            Socio("C1", limite: 1000m);
            Saldo("C1", 300m, 0m);
            Ped(1, "C1", 500m);
            PedLin(1, 1, 1m, 500m);

            var r = await _servicio.VerificarAsync("C1", 201m);

            Assert.False(r.Permitido);
            Assert.Equal("LIMITE", r.Motivo);
            Assert.Equal(1m, r.Exceso);
            Assert.Equal(201m, r.Monto);
            Assert.Equal(800m, r.Posicion!.Exposicion);
        }

        [Fact]
        public async Task Verificar_LimiteCero_SoloPermiteMontoCero()
        {
            Socio("C1", limite: 0m);
            Saldo("C1", 0m, 0m);

            var conMonto = await _servicio.VerificarAsync("C1", 0.01m);
            var sinMonto = await _servicio.VerificarAsync("C1", 0m);

            Assert.False(conMonto.Permitido);
            Assert.Equal("LIMITE", conMonto.Motivo);
            Assert.Equal(0.01m, conMonto.Exceso);
            Assert.True(sinMonto.Permitido);
        }

        [Fact]
        public async Task Verificar_MontoCero_PermitidoSinConsultarSaldos()
        {
            Socio("C1", limite: 0m, bloquear: "S");
            Saldo("C1", 500m, 100m);

            var r = await _servicio.VerificarAsync("C1", 0m);

            Assert.True(r.Permitido);
            Assert.Null(r.Motivo);
            Assert.Equal(0m, r.Monto);
            Assert.Equal(0m, r.Exceso);
            _saldoSocio.Verify(s => s.ObtenerAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
        }

        [Fact]
        public async Task Verificar_BloquearSiVencidoConVencido_VencidoConExcesoIgualAlMonto()
        {
            Socio("C1", limite: 100000m, bloquear: "S");
            Saldo("C1", 500m, 10m);

            var r = await _servicio.VerificarAsync("C1", 70m);

            Assert.False(r.Permitido);
            Assert.Equal("VENCIDO", r.Motivo);
            Assert.Equal(70m, r.Exceso);
            Assert.Equal(70m, r.Monto);
            Assert.Equal(10m, r.Posicion!.Vencido);
        }

        [Fact]
        public async Task Verificar_BloquearSiVencidoSinVencido_NoBloquea()
        {
            Socio("C1", bloquear: "S");
            Saldo("C1", 500m, 0m);

            var r = await _servicio.VerificarAsync("C1", 70m);

            Assert.True(r.Permitido);
            Assert.Null(r.Motivo);
        }

        [Fact]
        public async Task Verificar_InterruptorApagadoConVencidoAlto_SoloImportaElLimite()
        {
            Socio("C1", limite: 1000m, bloquear: "N");
            Saldo("C1", 100m, 100m);

            var permitido = await _servicio.VerificarAsync("C1", 100m);
            var excedido = await _servicio.VerificarAsync("C1", 1000m);

            Assert.True(permitido.Permitido);
            Assert.False(excedido.Permitido);
            Assert.Equal("LIMITE", excedido.Motivo);
            Assert.Equal(100m, excedido.Exceso); // 100 + 1000 - 1000
        }

        [Fact]
        public async Task Verificar_VencidoYLimiteFallan_VencidoTienePrioridad()
        {
            Socio("C1", limite: 100m, bloquear: "S");
            Saldo("C1", 500m, 50m);

            var r = await _servicio.VerificarAsync("C1", 30m);

            Assert.False(r.Permitido);
            Assert.Equal("VENCIDO", r.Motivo);
            Assert.Equal(30m, r.Exceso);
        }

        [Theory]
        [InlineData("P1")]
        [InlineData("NOEXISTE")]
        [InlineData("")]
        [InlineData(null)]
        public async Task Verificar_SocioNoClienteOInexistente_PermitidoSinConsultarSaldos(string? codigo)
        {
            Socio("P1", tipo: "P", limite: 0m);
            Saldo("P1", 500m, 500m);

            var r = await _servicio.VerificarAsync(codigo, 1000m);

            Assert.True(r.Permitido);
            Assert.Null(r.Motivo);
            Assert.Null(r.Posicion);
            Assert.Equal(1000m, r.Monto);
            Assert.Equal(0m, r.Exceso);
            _saldoSocio.Verify(s => s.ObtenerAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
        }

        // ---- 14. Monto neto del documento ----

        [Fact]
        public async Task CalcularMonto_FacturaSinBase_EsElTotalMenosElPagoInmediato()
        {
            var sinPago = await _servicio.CalcularMontoAsync(6, 500m, 0m, Array.Empty<LineaBaseRequest>());
            var conPago = await _servicio.CalcularMontoAsync(6, 500m, 500m, Array.Empty<LineaBaseRequest>());

            Assert.Equal(500m, sinPago);
            Assert.Equal(0m, conPago);
            // Sin lineas base no se consulta ningun origen.
            _repoPedido.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _repoPedidoDet.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _repoEntrega.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _repoEntregaDet.Verify(x => x.ObtenerTodoAsync(), Times.Never);
            _repoFacturaDet.Verify(x => x.ObtenerTodoAsync(), Times.Never);
        }

        [Fact]
        public async Task CalcularMonto_FacturaDeUnaEntrega_LiberaElValorNetoDeLaEntrega()
        {
            EntregaE1(); // 4 u, valor unitario neto 240 x 1.1 / 4 = 66

            var monto = await _servicio.CalcularMontoAsync(6, 264m, 0m,
                new[] { new LineaBaseRequest(5, 10, 1, null, 4m) });

            Assert.Equal(0m, monto);
        }

        [Fact]
        public async Task CalcularMonto_FacturaDeUnaEntregaConPagoInmediato_NoLiberaDeMas()
        {
            EntregaE1();

            // Lleva 2 de las 4 unidades: libera 132; el documento vale 150 y se cobra 10 en el acto.
            var monto = await _servicio.CalcularMontoAsync(6, 150m, 10m,
                new[] { new LineaBaseRequest(5, 10, 1, null, 2m) });

            Assert.Equal(8m, monto);
        }

        [Fact]
        public async Task CalcularMonto_EntregaDesdePedido_LiberaLoDelPedido()
        {
            PedidoP1(); // L1: 10 u, 600 -> unitario 66

            var igual = await _servicio.CalcularMontoAsync(5, 264m, 0m,
                new[] { new LineaBaseRequest(4, 1, 1, null, 4m) });
            var masCaro = await _servicio.CalcularMontoAsync(5, 300m, 0m,
                new[] { new LineaBaseRequest(4, 1, 1, null, 4m) });

            Assert.Equal(0m, igual);
            Assert.Equal(36m, masCaro);
        }

        [Fact]
        public async Task CalcularMonto_LineasConBaseCotizacionOServicio_NoLiberan()
        {
            PedidoP1();

            var monto = await _servicio.CalcularMontoAsync(6, 500m, 0m, new[]
            {
                new LineaBaseRequest(3, 1, 1, null, 4m),
                new LineaBaseRequest(70, 1, 1, null, 4m)
            });

            Assert.Equal(500m, monto);
        }

        [Fact]
        public async Task CalcularMonto_MezclaDeBases_SoloLiberanPedidoYEntrega()
        {
            PedidoP1();
            EntregaE1();

            // 1 u del pedido (66) + 2 u de la entrega (132) liberan 198; la linea con base 3 no libera nada.
            var monto = await _servicio.CalcularMontoAsync(6, 300m, 0m, new[]
            {
                new LineaBaseRequest(4, 1, 1, null, 1m),
                new LineaBaseRequest(5, 10, 1, null, 2m),
                new LineaBaseRequest(3, 1, 1, null, 9m)
            });

            Assert.Equal(102m, monto);
        }

        [Fact]
        public async Task CalcularMonto_DocumentoMenorQueLoLiberado_Cero()
        {
            EntregaE1();

            var monto = await _servicio.CalcularMontoAsync(6, 100m, 0m,
                new[] { new LineaBaseRequest(5, 10, 1, null, 4m) });

            Assert.Equal(0m, monto);
        }

        [Fact]
        public async Task CalcularMonto_OrigenOLineaInexistente_NoLibera()
        {
            EntregaE1();

            var monto = await _servicio.CalcularMontoAsync(6, 100m, 0m, new[]
            {
                new LineaBaseRequest(5, 999, 1, null, 4m),
                new LineaBaseRequest(5, 10, 9, null, 4m)
            });

            Assert.Equal(100m, monto);
        }

        [Fact]
        public async Task CalcularMonto_RedondeaDosDecimalesAwayFromZero()
        {
            var monto = await _servicio.CalcularMontoAsync(6, 10.005m, 0m, Array.Empty<LineaBaseRequest>());

            Assert.Equal(10.01m, monto);
        }
    }
}
