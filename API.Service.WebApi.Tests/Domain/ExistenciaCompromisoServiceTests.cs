using API.Domain.Core.Existencias;
using API.Domain.Entity.Models;
using API.Domain.Interface.Existencias;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class ExistenciaCompromisoServiceTests
    {
        private readonly Mock<IRepositorioGenerico<Pedido, int>> _ped = new();
        private readonly Mock<IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)>> _pedDet = new();
        private readonly Mock<IRepositorioGenerico<Entrega, int>> _ent = new();
        private readonly Mock<IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)>> _entDet = new();
        private readonly Mock<IRepositorioGenerico<Factura, int>> _fac = new();
        private readonly Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>> _facDet = new();
        private readonly Mock<IRepositorioGenerico<PedidoCompra, int>> _pedC = new();
        private readonly Mock<IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)>> _pedCDet = new();
        private readonly Mock<IRepositorioGenerico<EntregaCompra, int>> _entC = new();
        private readonly Mock<IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)>> _entCDet = new();
        private readonly Mock<IRepositorioGenerico<FacturaCompra, int>> _facC = new();
        private readonly Mock<IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)>> _facCDet = new();
        private readonly Mock<IRepositorioGenerico<Articulo, string>> _art = new();
        private readonly Mock<IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)>> _exi = new();
        private readonly Mock<ILogger<ExistenciaCompromisoService>> _log = new();

        private readonly List<Pedido> _pedidos = new();
        private readonly List<PedidoDetalle> _pedLineas = new();
        private readonly List<Entrega> _entregas = new();
        private readonly List<EntregaDetalle> _entLineas = new();
        private readonly List<Factura> _facturas = new();
        private readonly List<FacturaDetalle> _facLineas = new();
        private readonly List<PedidoCompra> _pedidosC = new();
        private readonly List<PedidoCompraDetalle> _pedCLineas = new();
        private readonly List<EntregaCompra> _entregasC = new();
        private readonly List<EntregaCompraDetalle> _entCLineas = new();
        private readonly List<FacturaCompra> _facturasC = new();
        private readonly List<FacturaCompraDetalle> _facCLineas = new();
        private readonly List<Articulo> _articulos = new();
        private readonly List<ExistenciaArticulo> _existencias = new();

        private readonly ExistenciaCompromisoService _svc;

        private static readonly ParExistencia A1_01 = new("A1", "01");

        public ExistenciaCompromisoServiceTests()
        {
            _ped.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _pedidos.AsAsyncQueryable());
            _pedDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _pedLineas.AsAsyncQueryable());
            _ent.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _entregas.AsAsyncQueryable());
            _entDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _entLineas.AsAsyncQueryable());
            _fac.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _facturas.AsAsyncQueryable());
            _facDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _facLineas.AsAsyncQueryable());
            _pedC.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _pedidosC.AsAsyncQueryable());
            _pedCDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _pedCLineas.AsAsyncQueryable());
            _entC.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _entregasC.AsAsyncQueryable());
            _entCDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _entCLineas.AsAsyncQueryable());
            _facC.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _facturasC.AsAsyncQueryable());
            _facCDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _facCLineas.AsAsyncQueryable());
            _art.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _articulos.AsAsyncQueryable());
            _exi.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _existencias.AsAsyncQueryable());
            _exi.Setup(r => r.ObtenerAsync(It.IsAny<(string, string)>()))
                .ReturnsAsync(((string CodArticulo, string CodAlmacen) k) => _existencias.FirstOrDefault(e => e.CodArticulo == k.CodArticulo && e.CodAlmacen == k.CodAlmacen));
            _exi.Setup(r => r.InsertarAsync(It.IsAny<ExistenciaArticulo>()))
                .ReturnsAsync((ExistenciaArticulo e) => { _existencias.Add(e); return e; });
            _exi.Setup(r => r.ActualizarAsync(It.IsAny<(string, string)>(), It.IsAny<ExistenciaArticulo>())).ReturnsAsync(true);

            _articulos.Add(new Articulo { Codigo = "A1", ArticuloInventario = "S" });

            _svc = new ExistenciaCompromisoService(_ped.Object, _pedDet.Object, _ent.Object, _entDet.Object, _fac.Object, _facDet.Object,
                _pedC.Object, _pedCDet.Object, _entC.Object, _entCDet.Object, _facC.Object, _facCDet.Object,
                _art.Object, _exi.Object, _log.Object);
        }

        // ---------- fábricas de datos ----------

        private void Art(string codigo, string? inventario = "S") => _articulos.Add(new Articulo { Codigo = codigo, ArticuloInventario = inventario });

        private void Pedido(int entry, string? cancelado = "N", string? estadoDoc = "A") => _pedidos.Add(new Pedido { Entry = entry, Cancelado = cancelado, EstadoDoc = estadoDoc });
        private void LinPedido(int entry, int no, decimal cant, string art = "A1", string? alm = "01", decimal factor = 1m) =>
            _pedLineas.Add(new PedidoDetalle { Entry = entry, NoLinea = no, CodArticulo = art, CodAlmacen = alm, Cantidad = cant, FactorUom = factor });
        private void Entrega(int entry, string? cancelado = "N", string? estadoDoc = "A") => _entregas.Add(new Entrega { Entry = entry, Cancelado = cancelado, EstadoDoc = estadoDoc });
        private void LinEntrega(int entry, int no, decimal cant, int? baseEntry, int? baseLinea, int? baseTipo = 4, string art = "A1", string? alm = "01") =>
            _entLineas.Add(new EntregaDetalle { Entry = entry, NoLinea = no, CodArticulo = art, CodAlmacen = alm, Cantidad = cant, BaseTipo = baseTipo, BaseEntry = baseEntry, BaseLinea = baseLinea });
        private void Factura(int entry, string? cancelado = "N", string? estadoDoc = "A") => _facturas.Add(new Factura { Entry = entry, Cancelado = cancelado, EstadoDoc = estadoDoc });
        private void LinFactura(int entry, int no, decimal cant, int? baseEntry, int? baseLinea, int? baseTipo = 4, string art = "A1", string? alm = "01") =>
            _facLineas.Add(new FacturaDetalle { Entry = entry, NoLinea = no, CodArticulo = art, CodAlmacen = alm, Cantidad = cant, BaseTipo = baseTipo, BaseEntry = baseEntry, BaseLinea = baseLinea });

        private void PedidoC(int entry, string? cancelado = "N", string? estadoDoc = "A") => _pedidosC.Add(new PedidoCompra { Entry = entry, Cancelado = cancelado, EstadoDoc = estadoDoc });
        private void LinPedidoC(int entry, int no, decimal cant, string art = "A1", string? alm = "01", decimal factor = 1m) =>
            _pedCLineas.Add(new PedidoCompraDetalle { Entry = entry, NoLinea = no, CodArticulo = art, CodAlmacen = alm, Cantidad = cant, FactorUom = factor });
        private void EntregaC(int entry, string? cancelado = "N", string? estadoDoc = "A") => _entregasC.Add(new EntregaCompra { Entry = entry, Cancelado = cancelado, EstadoDoc = estadoDoc });
        private void LinEntregaC(int entry, int no, decimal cant, int? baseEntry, int? baseLinea, int? baseTipo = 11, string art = "A1", string? alm = "01") =>
            _entCLineas.Add(new EntregaCompraDetalle { Entry = entry, NoLinea = no, CodArticulo = art, CodAlmacen = alm, Cantidad = cant, BaseTipo = baseTipo, BaseEntry = baseEntry, BaseLinea = baseLinea });
        private void FacturaC(int entry, string? cancelado = "N", string? estadoDoc = "A") => _facturasC.Add(new FacturaCompra { Entry = entry, Cancelado = cancelado, EstadoDoc = estadoDoc });
        private void LinFacturaC(int entry, int no, decimal cant, int? baseEntry, int? baseLinea, int? baseTipo = 11, string art = "A1", string? alm = "01") =>
            _facCLineas.Add(new FacturaCompraDetalle { Entry = entry, NoLinea = no, CodArticulo = art, CodAlmacen = alm, Cantidad = cant, BaseTipo = baseTipo, BaseEntry = baseEntry, BaseLinea = baseLinea });

        private ExistenciaArticulo Exist(string art, string alm, decimal disp = 0m, decimal comp = 0m, decimal ped = 0m)
        {
            var e = new ExistenciaArticulo { CodArticulo = art, CodAlmacen = alm, Disponible = disp, Comprometido = comp, Pedido = ped };
            _existencias.Add(e);
            return e;
        }

        private ExistenciaArticulo? Fila(string art, string alm) => _existencias.FirstOrDefault(e => e.CodArticulo == art && e.CodAlmacen == alm);

        // ---------- 1-2: ventas ----------

        [Fact]
        public async Task PedidoDeVentaActivo_Compromete10()
        {
            Pedido(1); LinPedido(1, 1, 10m);
            Exist("A1", "01", disp: 100m);

            await _svc.RecalcularAsync(new[] { A1_01 });

            var f = Fila("A1", "01")!;
            Assert.Equal(10m, f.Comprometido);
            Assert.Equal(0m, f.Pedido);
            Assert.Equal(100m, f.Disponible);
            _exi.Verify(r => r.ActualizarAsync(It.Is<(string, string)>(k => k.Item1 == "A1" && k.Item2 == "01"), f), Times.Once);
        }

        [Fact]
        public async Task EntregaYFactura_ConsumenElPedido()
        {
            Pedido(1); LinPedido(1, 1, 10m);
            Exist("A1", "01");
            Entrega(1); LinEntrega(1, 1, 4m, 1, 1);

            await _svc.RecalcularAsync(new[] { A1_01 });
            Assert.Equal(6m, Fila("A1", "01")!.Comprometido);

            Factura(1); LinFactura(1, 1, 6m, 1, 1);
            await _svc.RecalcularAsync(new[] { A1_01 });
            Assert.Equal(0m, Fila("A1", "01")!.Comprometido);
        }

        // ---------- 3: documentos cancelados no consumen ----------

        [Fact]
        public async Task EntregaCancelada_NoConsume()
        {
            Pedido(1); LinPedido(1, 1, 10m);
            Exist("A1", "01");
            Entrega(1, cancelado: "S"); LinEntrega(1, 1, 4m, 1, 1);

            await _svc.RecalcularAsync(new[] { A1_01 });

            Assert.Equal(10m, Fila("A1", "01")!.Comprometido);
        }

        [Fact]
        public async Task EntregaConEstadoDocC_NoConsume()
        {
            Pedido(1); LinPedido(1, 1, 10m);
            Exist("A1", "01");
            Entrega(1, estadoDoc: "C"); LinEntrega(1, 1, 4m, 1, 1);

            await _svc.RecalcularAsync(new[] { A1_01 });

            Assert.Equal(10m, Fila("A1", "01")!.Comprometido);
        }

        [Fact]
        public async Task FacturaCancelada_NoConsume()
        {
            Pedido(1); LinPedido(1, 1, 10m);
            Exist("A1", "01");
            Factura(1, cancelado: "S"); LinFactura(1, 1, 10m, 1, 1);

            await _svc.RecalcularAsync(new[] { A1_01 });

            Assert.Equal(10m, Fila("A1", "01")!.Comprometido);
        }

        // ---------- 4: pedido no activo ----------

        [Fact]
        public async Task PedidoCancelado_DejaEnCeroLaFilaPrevia()
        {
            Pedido(1, cancelado: "S"); LinPedido(1, 1, 10m);
            Exist("A1", "01", comp: 10m);

            await _svc.RecalcularAsync(new[] { A1_01 });

            Assert.Equal(0m, Fila("A1", "01")!.Comprometido);
        }

        [Fact]
        public async Task PedidoConEstadoDocC_DejaEnCeroLaFilaPrevia()
        {
            Pedido(1, estadoDoc: "C"); LinPedido(1, 1, 10m);
            Exist("A1", "01", comp: 10m);

            await _svc.RecalcularAsync(new[] { A1_01 });

            Assert.Equal(0m, Fila("A1", "01")!.Comprometido);
        }

        // ---------- 5: varios pedidos y pares ----------

        [Fact]
        public async Task DosPedidosDelMismoPar_Suman15()
        {
            Pedido(1); LinPedido(1, 1, 10m);
            Pedido(2); LinPedido(2, 1, 5m);
            Exist("A1", "01");

            await _svc.RecalcularAsync(new[] { A1_01 });

            Assert.Equal(15m, Fila("A1", "01")!.Comprometido);
        }

        [Fact]
        public async Task ParesDistintos_SonIndependientes()
        {
            Pedido(1); LinPedido(1, 1, 10m, alm: "01");
            Pedido(2); LinPedido(2, 1, 7m, alm: "02");
            Exist("A1", "01"); Exist("A1", "02");

            await _svc.RecalcularAsync(new[] { A1_01, new ParExistencia("A1", "02") });

            Assert.Equal(10m, Fila("A1", "01")!.Comprometido);
            Assert.Equal(7m, Fila("A1", "02")!.Comprometido);
        }

        [Fact]
        public async Task EntregaDeOtroAlmacen_LiberaElComprometidoDelAlmacenDelPedido()
        {
            Pedido(1); LinPedido(1, 1, 10m, alm: "01");
            Exist("A1", "01"); Exist("A1", "02");
            Entrega(1); LinEntrega(1, 1, 4m, 1, 1, alm: "02");

            await _svc.RecalcularAsync(new[] { A1_01, new ParExistencia("A1", "02") });

            Assert.Equal(6m, Fila("A1", "01")!.Comprometido);
            Assert.Equal(0m, Fila("A1", "02")!.Comprometido);
        }

        // ---------- 6: unidad con factor ----------

        [Fact]
        public async Task UnidadConFactor12_ConvierteAUnidadBase()
        {
            Pedido(1); LinPedido(1, 1, 3m, factor: 12m);
            Exist("A1", "01");
            Entrega(1); LinEntrega(1, 1, 1m, 1, 1);

            await _svc.RecalcularAsync(new[] { A1_01 });

            Assert.Equal(24m, Fila("A1", "01")!.Comprometido);
        }

        [Fact]
        public async Task FactorCeroONegativo_SeTrataComoUno()
        {
            Pedido(1); LinPedido(1, 1, 5m, factor: 0m); LinPedido(1, 2, 2m, factor: -3m);
            Exist("A1", "01");

            await _svc.RecalcularAsync(new[] { A1_01 });

            Assert.Equal(7m, Fila("A1", "01")!.Comprometido);
        }

        [Fact]
        public async Task LineaSobreconsumida_AporteCero_YNoRestaALasDemas()
        {
            Pedido(1); LinPedido(1, 1, 10m);
            Pedido(2); LinPedido(2, 1, 4m);
            Exist("A1", "01");
            Entrega(1); LinEntrega(1, 1, 8m, 1, 1);
            Factura(1); LinFactura(1, 1, 4m, 1, 1);   // 12 consumidos de 10

            await _svc.RecalcularAsync(new[] { A1_01 });

            Assert.Equal(4m, Fila("A1", "01")!.Comprometido);
        }

        [Fact]
        public async Task ConsumoDeVentasYComprasNoSeMezclan()
        {
            Pedido(1); LinPedido(1, 1, 10m);
            PedidoC(1); LinPedidoC(1, 1, 10m);
            Exist("A1", "01");
            // una entrega de compra con BaseTipo 4 y una entrega de venta con BaseTipo 11 no consumen nada
            EntregaC(1); LinEntregaC(1, 1, 3m, 1, 1, baseTipo: 4);
            Entrega(1); LinEntrega(1, 1, 3m, 1, 1, baseTipo: 11);

            await _svc.RecalcularAsync(new[] { A1_01 });

            var f = Fila("A1", "01")!;
            Assert.Equal(10m, f.Comprometido);
            Assert.Equal(10m, f.Pedido);
        }

        [Fact]
        public async Task EntregaDeCompraConEstadoDocC_SiConsume_PorLaReglaDeCompras()
        {
            PedidoC(1); LinPedidoC(1, 1, 20m);
            Exist("A1", "01");
            EntregaC(1, estadoDoc: "C"); LinEntregaC(1, 1, 5m, 1, 1);

            await _svc.RecalcularAsync(new[] { A1_01 });

            Assert.Equal(15m, Fila("A1", "01")!.Pedido);
        }

        // ---------- 7: lo que no compromete ----------

        [Fact]
        public async Task ArticuloNoInventariable_SeIgnora()
        {
            Art("S1", "N");
            Pedido(1); LinPedido(1, 1, 10m, art: "S1");
            Exist("S1", "01", comp: 3m);

            await _svc.RecalcularAsync(new[] { new ParExistencia("S1", "01") });

            Assert.Equal(0m, Fila("S1", "01")!.Comprometido);
        }

        [Fact]
        public async Task LineaSinAlmacen_SeIgnora()
        {
            Pedido(1); LinPedido(1, 1, 10m, alm: null); LinPedido(1, 2, 9m, alm: "");
            Exist("A1", "01");

            await _svc.RecalcularAsync(new[] { A1_01 });

            Assert.Equal(0m, Fila("A1", "01")!.Comprometido);
        }

        // ---------- 8: compras ----------

        [Fact]
        public async Task Compras_PedidoEntregaYFactura()
        {
            PedidoC(1); LinPedidoC(1, 1, 20m);
            Exist("A1", "01");
            EntregaC(1); LinEntregaC(1, 1, 5m, 1, 1);

            await _svc.RecalcularAsync(new[] { A1_01 });
            Assert.Equal(15m, Fila("A1", "01")!.Pedido);
            Assert.Equal(0m, Fila("A1", "01")!.Comprometido);

            FacturaC(1); LinFacturaC(1, 1, 15m, 1, 1);
            await _svc.RecalcularAsync(new[] { A1_01 });
            Assert.Equal(0m, Fila("A1", "01")!.Pedido);
        }

        [Fact]
        public async Task EntregaDeCompraCancelada_NoConsume()
        {
            PedidoC(1); LinPedidoC(1, 1, 20m);
            Exist("A1", "01");
            EntregaC(1, cancelado: "S"); LinEntregaC(1, 1, 5m, 1, 1);

            await _svc.RecalcularAsync(new[] { A1_01 });

            Assert.Equal(20m, Fila("A1", "01")!.Pedido);
        }

        [Fact]
        public async Task PedidoDeCompraCancelado_DejaEnCero()
        {
            PedidoC(1, cancelado: "S"); LinPedidoC(1, 1, 20m);
            Exist("A1", "01", ped: 20m);

            await _svc.RecalcularAsync(new[] { A1_01 });

            Assert.Equal(0m, Fila("A1", "01")!.Pedido);
        }

        // ---------- 9: ventas y compras del mismo par ----------

        [Fact]
        public async Task MismoPar_ComprometidoYPedidoSeCalculanPorSeparado()
        {
            Pedido(1); LinPedido(1, 1, 10m);
            PedidoC(1); LinPedidoC(1, 1, 20m);
            Exist("A1", "01");
            Entrega(1); LinEntrega(1, 1, 4m, 1, 1);

            await _svc.RecalcularAsync(new[] { A1_01 });

            var f = Fila("A1", "01")!;
            Assert.Equal(6m, f.Comprometido);
            Assert.Equal(20m, f.Pedido);
        }

        // ---------- 10: escritura ----------

        [Fact]
        public async Task CreaLaFilaDeExistencias_SiFaltaYHayValor()
        {
            Pedido(1); LinPedido(1, 1, 10m);
            PedidoC(1); LinPedidoC(1, 1, 20m);

            await _svc.RecalcularAsync(new[] { A1_01 });

            var f = Fila("A1", "01")!;
            Assert.NotNull(f);
            Assert.Equal(0m, f.Disponible);
            Assert.Equal(10m, f.Comprometido);
            Assert.Equal(20m, f.Pedido);
            Assert.True((DateTime.Now - f.FechaActualizacion).TotalMinutes < 1);
            _exi.Verify(r => r.InsertarAsync(It.IsAny<ExistenciaArticulo>()), Times.Once);
        }

        [Fact]
        public async Task NoCreaLaFila_SiAmbosValoresSonCero()
        {
            await _svc.RecalcularAsync(new[] { A1_01 });

            Assert.Empty(_existencias);
            _exi.Verify(r => r.InsertarAsync(It.IsAny<ExistenciaArticulo>()), Times.Never);
            _exi.Verify(r => r.ActualizarAsync(It.IsAny<(string, string)>(), It.IsAny<ExistenciaArticulo>()), Times.Never);
        }

        [Fact]
        public async Task NoEscribe_SiElValorNoCambio()
        {
            Pedido(1); LinPedido(1, 1, 10m);
            Exist("A1", "01", comp: 10m);

            await _svc.RecalcularAsync(new[] { A1_01 });

            _exi.Verify(r => r.ActualizarAsync(It.IsAny<(string, string)>(), It.IsAny<ExistenciaArticulo>()), Times.Never);
            _exi.Verify(r => r.InsertarAsync(It.IsAny<ExistenciaArticulo>()), Times.Never);
        }

        [Fact]
        public async Task ActualizaFechaYNoTocaDisponible_AlCambiar()
        {
            Pedido(1); LinPedido(1, 1, 10m);
            var e = Exist("A1", "01", disp: 55m);
            e.FechaActualizacion = new DateTime(2020, 1, 1);

            await _svc.RecalcularAsync(new[] { A1_01 });

            Assert.Equal(55m, e.Disponible);
            Assert.True(e.FechaActualizacion > new DateTime(2020, 1, 1));
        }

        [Fact]
        public async Task CodigosSolicitadosConEspacios_SeRecortan()
        {
            Pedido(1); LinPedido(1, 1, 10m);
            Exist("A1", "01");

            await _svc.RecalcularAsync(new[] { new ParExistencia(" A1", "01 ") });

            Assert.Equal(10m, Fila("A1", "01")!.Comprometido);
        }

        [Fact]
        public async Task ParesVaciosONulos_SeIgnoranSinConsultar()
        {
            await _svc.RecalcularAsync(new[] { new ParExistencia("", "01"), new ParExistencia("A1", " "), new ParExistencia(null!, null!) });

            _pedDet.Verify(r => r.ObtenerTodoAsync(), Times.Never);
            _exi.Verify(r => r.ObtenerAsync(It.IsAny<(string, string)>()), Times.Never);
        }

        [Fact]
        public async Task MasDe1000Pedidos_ElConsumoSeSumaEnUnaSolaConsulta()
        {
            for (var i = 1; i <= 1201; i++) { Pedido(i); LinPedido(i, 1, 1m); }
            Entrega(1); LinEntrega(1, 1, 1m, 1201, 1);
            Factura(1); LinFactura(1, 1, 1m, 1200, 1);
            Exist("A1", "01");

            await _svc.RecalcularAsync(new[] { A1_01 });

            Assert.Equal(1199m, Fila("A1", "01")!.Comprometido);
            // El consumo va dentro de la misma consulta de las lineas (subconsulta), no en listas de entries por lotes.
            _entDet.Verify(r => r.ObtenerTodoAsync(), Times.Once);
            _facDet.Verify(r => r.ObtenerTodoAsync(), Times.Once);
        }

        [Fact]
        public async Task RecalcularTodo_ConHistorialConsumido_SoloSumaLoAbierto()
        {
            // Historial de pedidos totalmente entregados y sin cerrar: no comprometen nada; una sola linea abierta.
            for (var i = 1; i <= 1500; i++) { Pedido(i); LinPedido(i, 1, 2m); Entrega(i); LinEntrega(i, 1, 2m, i, 1); }
            Pedido(2000); LinPedido(2000, 1, 7m);
            Exist("A1", "01", comp: 99m);

            var r = await _svc.RecalcularTodoAsync();

            Assert.Equal(new ResultadoRecalculo(1, 1), r);
            Assert.Equal(7m, Fila("A1", "01")!.Comprometido);
            _entDet.Verify(x => x.ObtenerTodoAsync(), Times.Once);
        }

        // ---------- 11: recalcular todo ----------

        [Fact]
        public async Task RecalcularTodo_DejaEnCeroLosParesSinPedidosYCuentaFilas()
        {
            Art("A2"); Art("A3");
            // A1: pedido abierto de 10 y fila con 10 -> sin cambio
            Pedido(1); LinPedido(1, 1, 10m, art: "A1");
            Exist("A1", "01", comp: 10m);
            // A2: pedido abierto de 8 y fila con 3 -> corregida
            Pedido(2); LinPedido(2, 1, 8m, art: "A2");
            Exist("A2", "01", comp: 3m);
            // A3: sin pedidos y fila con comprometido 5 y pedido 2 -> en 0
            Exist("A3", "01", comp: 5m, ped: 2m);
            // fila sin valores: no se revisa
            Exist("A1", "02");

            var r = await _svc.RecalcularTodoAsync();

            Assert.Equal(3, r.Filas);
            Assert.Equal(2, r.Corregidas);
            Assert.Equal(10m, Fila("A1", "01")!.Comprometido);
            Assert.Equal(8m, Fila("A2", "01")!.Comprometido);
            Assert.Equal(0m, Fila("A3", "01")!.Comprometido);
            Assert.Equal(0m, Fila("A3", "01")!.Pedido);
            _exi.Verify(x => x.ActualizarAsync(It.IsAny<(string, string)>(), It.IsAny<ExistenciaArticulo>()), Times.Exactly(2));
        }

        [Fact]
        public async Task RecalcularTodo_CreaFilasQueFaltanYEsIdempotente()
        {
            PedidoC(1); LinPedidoC(1, 1, 20m);

            var primera = await _svc.RecalcularTodoAsync();
            var segunda = await _svc.RecalcularTodoAsync();

            Assert.Equal(new ResultadoRecalculo(1, 1), primera);
            Assert.Equal(new ResultadoRecalculo(1, 0), segunda);
            Assert.Equal(20m, Fila("A1", "01")!.Pedido);
        }

        [Fact]
        public async Task RecalcularTodo_SinNada_DevuelveCeros()
        {
            var r = await _svc.RecalcularTodoAsync();
            Assert.Equal(new ResultadoRecalculo(0, 0), r);
        }

        // ---------- 12: pares por documento ----------

        [Fact]
        public async Task ObtenerPares_DeUnaEntregaConBaseDePedido_IncluyeLosDelPedidoSinDuplicados()
        {
            Art("A2");
            Pedido(1); LinPedido(1, 1, 10m, art: "A1", alm: "01"); LinPedido(1, 2, 5m, art: "A2", alm: "03");
            Entrega(1);
            LinEntrega(1, 1, 4m, 1, 1, art: "A1", alm: "02");   // base: A1/01 (pedido), propia A1/02
            LinEntrega(1, 2, 2m, 1, 2, art: "A2", alm: "03");   // base A2/03 = propia A2/03 (duplicado)

            var pares = await _svc.ObtenerParesAsync(TipoDocCompromiso.Entrega, 1);

            Assert.Equal(3, pares.Count);
            Assert.Contains(new ParExistencia("A1", "02"), pares);
            Assert.Contains(new ParExistencia("A1", "01"), pares);
            Assert.Contains(new ParExistencia("A2", "03"), pares);
        }

        [Fact]
        public async Task ObtenerPares_DeDocumentoInexistente_DevuelveVacio()
        {
            Assert.Empty(await _svc.ObtenerParesAsync(TipoDocCompromiso.Entrega, 99));
            Assert.Empty(await _svc.ObtenerParesAsync(TipoDocCompromiso.Pedido, 99));
            Assert.Empty(await _svc.ObtenerParesAsync(TipoDocCompromiso.FacturaCompra, 99));
            Assert.Empty(await _svc.ObtenerParesAsync(77, 1));
        }

        [Fact]
        public async Task ObtenerPares_DeUnPedido_SoloLosDeSusLineasInventariables()
        {
            Art("S1", "N");
            Pedido(1); LinPedido(1, 1, 10m); LinPedido(1, 2, 3m, art: "S1"); LinPedido(1, 3, 3m, alm: null);

            var pares = await _svc.ObtenerParesAsync(TipoDocCompromiso.Pedido, 1);

            Assert.Equal(new[] { A1_01 }, pares);
        }

        [Fact]
        public async Task ObtenerPares_DeUnPedidoDeCompra_Y_DeFacturaDeCompraConBase()
        {
            Art("A2");
            PedidoC(1); LinPedidoC(1, 1, 10m, art: "A2", alm: "05");
            FacturaC(1); LinFacturaC(1, 1, 4m, 1, 1, art: "A1", alm: "01");

            var pedido = await _svc.ObtenerParesAsync(TipoDocCompromiso.PedidoCompra, 1);
            var factura = await _svc.ObtenerParesAsync(TipoDocCompromiso.FacturaCompra, 1);

            Assert.Equal(new[] { new ParExistencia("A2", "05") }, pedido);
            Assert.Equal(2, factura.Count);
            Assert.Contains(A1_01, factura);
            Assert.Contains(new ParExistencia("A2", "05"), factura);
        }

        [Fact]
        public async Task FacturaSinBase_Pos_DevuelveSoloSusLineas_Y_RecalcularPorDocumentoNoConsultaPedidos()
        {
            Factura(1); LinFactura(1, 1, 2m, null, null, baseTipo: null);

            var pares = await _svc.ObtenerParesAsync(TipoDocCompromiso.Factura, 1);
            Assert.Equal(new[] { A1_01 }, pares);

            await _svc.RecalcularPorDocumentoAsync(TipoDocCompromiso.Factura, 1);

            _ped.Verify(r => r.ObtenerTodoAsync(), Times.Never);
            _pedDet.Verify(r => r.ObtenerTodoAsync(), Times.Never);
            _exi.Verify(r => r.ObtenerAsync(It.IsAny<(string, string)>()), Times.Never);
        }

        [Fact]
        public async Task RecalcularPorDocumento_DePedido_RecalculaSusPares()
        {
            Pedido(1); LinPedido(1, 1, 10m);
            Exist("A1", "01");

            await _svc.RecalcularPorDocumentoAsync(TipoDocCompromiso.Pedido, 1);

            Assert.Equal(10m, Fila("A1", "01")!.Comprometido);
        }

        [Fact]
        public async Task RecalcularPorDocumento_DeEntregaConPedidoBase_RecalculaLosParesDelPedido()
        {
            Pedido(1); LinPedido(1, 1, 10m);
            Exist("A1", "01", comp: 10m);
            Entrega(1); LinEntrega(1, 1, 4m, 1, 1);

            await _svc.RecalcularPorDocumentoAsync(TipoDocCompromiso.Entrega, 1);

            Assert.Equal(6m, Fila("A1", "01")!.Comprometido);
        }

        [Fact]
        public async Task RecalcularPorDocumento_DeEntregaDeCompraConBase_RecalculaPedido()
        {
            PedidoC(1); LinPedidoC(1, 1, 20m);
            Exist("A1", "01", ped: 20m);
            EntregaC(1); LinEntregaC(1, 1, 5m, 1, 1);

            await _svc.RecalcularPorDocumentoAsync(TipoDocCompromiso.EntregaCompra, 1);

            Assert.Equal(15m, Fila("A1", "01")!.Pedido);
        }

        // ---------- 13: envoltorios seguros ----------

        [Fact]
        public async Task NotificarDocumento_NoLanzaYRegistra()
        {
            _pedDet.Setup(r => r.ObtenerTodoAsync()).ThrowsAsync(new InvalidOperationException("falla de base"));

            await _svc.NotificarDocumentoAsync(TipoDocCompromiso.Pedido, 1);

            VerificarErrorRegistrado();
        }

        [Fact]
        public async Task NotificarPares_NoLanzaYRegistra()
        {
            _ped.Setup(r => r.ObtenerTodoAsync()).ThrowsAsync(new InvalidOperationException("falla de base"));
            _pedDet.Setup(r => r.ObtenerTodoAsync()).ThrowsAsync(new InvalidOperationException("falla de base"));

            await _svc.NotificarParesAsync(new[] { A1_01 });

            VerificarErrorRegistrado();
        }

        [Fact]
        public async Task Notificar_ConParesNulosOVacios_NoHaceNada()
        {
            await _svc.NotificarParesAsync(null!);
            await _svc.NotificarParesAsync(Array.Empty<ParExistencia>());

            _ped.Verify(r => r.ObtenerTodoAsync(), Times.Never);
            _articulosNoConsultados();
            _log.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Notificar_CuandoTodoVaBien_NoRegistraError()
        {
            Pedido(1); LinPedido(1, 1, 10m);
            Exist("A1", "01");

            await _svc.NotificarDocumentoAsync(TipoDocCompromiso.Pedido, 1);
            await _svc.NotificarParesAsync(new[] { A1_01 });

            Assert.Equal(10m, Fila("A1", "01")!.Comprometido);
            _log.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
        }

        // ---------- limpieza del rastreador tras una escritura fallida ----------

        [Fact]
        public async Task NotificarPares_SiFallaElInsert_NoLanzaDescartaPendientesYRegistra()
        {
            Pedido(1); LinPedido(1, 1, 10m);
            _exi.Setup(r => r.InsertarAsync(It.IsAny<ExistenciaArticulo>())).ThrowsAsync(new InvalidOperationException("llave duplicada"));

            await _svc.NotificarParesAsync(new[] { A1_01 });

            _exi.Verify(r => r.DescartarCambiosPendientes(), Times.Once);
            VerificarErrorRegistrado();
        }

        [Fact]
        public async Task NotificarDocumento_SiFallaElUpdate_NoLanzaDescartaPendientesYRegistra()
        {
            Pedido(1); LinPedido(1, 1, 10m);
            Exist("A1", "01", disp: 5m);
            _exi.Setup(r => r.ActualizarAsync(It.IsAny<(string, string)>(), It.IsAny<ExistenciaArticulo>())).ThrowsAsync(new InvalidOperationException("concurrencia"));

            await _svc.NotificarDocumentoAsync(TipoDocCompromiso.Pedido, 1);

            _exi.Verify(r => r.DescartarCambiosPendientes(), Times.Once);
            VerificarErrorRegistrado();
        }

        [Fact]
        public async Task Recalcular_SiFallaLaEscritura_DescartaPendientesYRelanza()
        {
            Pedido(1); LinPedido(1, 1, 10m);
            _exi.Setup(r => r.InsertarAsync(It.IsAny<ExistenciaArticulo>())).ThrowsAsync(new InvalidOperationException("llave duplicada"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => _svc.RecalcularAsync(new[] { A1_01 }));

            _exi.Verify(r => r.DescartarCambiosPendientes(), Times.Once);
        }

        [Fact]
        public async Task NoDescartaPendientes_CuandoLaEscrituraTieneExito()
        {
            Pedido(1); LinPedido(1, 1, 10m);
            Exist("A1", "01");

            await _svc.NotificarParesAsync(new[] { A1_01 });
            await _svc.RecalcularTodoAsync();

            _exi.Verify(r => r.DescartarCambiosPendientes(), Times.Never);
        }

        private void _articulosNoConsultados() => _art.Verify(r => r.ObtenerTodoAsync(), Times.Never);

        private void VerificarErrorRegistrado() =>
            _log.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}
