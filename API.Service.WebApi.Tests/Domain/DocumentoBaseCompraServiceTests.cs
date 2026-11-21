using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class DocumentoBaseCompraServiceTests
    {
        private readonly Mock<IRepositorioGenerico<PedidoCompra, int>> _ped = new();
        private readonly Mock<IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)>> _pedDet = new();
        private readonly Mock<IRepositorioGenerico<EntregaCompra, int>> _ent = new();
        private readonly Mock<IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)>> _entDet = new();
        private readonly Mock<IRepositorioGenerico<FacturaCompra, int>> _fac = new();
        private readonly Mock<IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)>> _facDet = new();

        private readonly List<PedidoCompra> _pedidos = new();
        private readonly List<PedidoCompraDetalle> _pedLineas = new();
        private readonly List<EntregaCompra> _entregas = new();
        private readonly List<EntregaCompraDetalle> _entLineas = new();
        private readonly List<FacturaCompra> _facturas = new();
        private readonly List<FacturaCompraDetalle> _facLineas = new();

        private readonly DocumentoBaseCompraService _svc;

        public DocumentoBaseCompraServiceTests()
        {
            _ped.Setup(r => r.ObtenerAsync(It.IsAny<int>())).ReturnsAsync((int id) => _pedidos.FirstOrDefault(x => x.Entry == id)!);
            _ent.Setup(r => r.ObtenerAsync(It.IsAny<int>())).ReturnsAsync((int id) => _entregas.FirstOrDefault(x => x.Entry == id)!);
            _fac.Setup(r => r.ObtenerAsync(It.IsAny<int>())).ReturnsAsync((int id) => _facturas.FirstOrDefault(x => x.Entry == id)!);
            _pedDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _pedLineas.AsAsyncQueryable());
            _entDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _entLineas.AsAsyncQueryable());
            _facDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _facLineas.AsAsyncQueryable());

            _svc = new DocumentoBaseCompraService(_ped.Object, _pedDet.Object, _ent.Object, _entDet.Object, _fac.Object, _facDet.Object);
        }

        // ---------- fábricas de datos ----------

        private PedidoCompra Ped(int entry, int numDoc, string sn = "P1", string? estadoDoc = "A", string? cancelado = "N")
        {
            var p = new PedidoCompra { Entry = entry, NumDoc = numDoc, CodigoSn = sn, NombreSn = "Proveedor", Direccion = "Dir", Nit = "1234567-8",
                MonedaDoc = "QTZ", PrctjeDesc = 0m, PrctjeImpuesto = 12m, Comentario = "nota", EstadoDoc = estadoDoc, Cancelado = cancelado };
            _pedidos.Add(p);
            return p;
        }

        private void LineaPed(int entry, int noLinea, string art, decimal cant, decimal factor = 1m, int? uom = null) =>
            _pedLineas.Add(new PedidoCompraDetalle { Entry = entry, NoLinea = noLinea, CodArticulo = art, Descripcion = "Desc " + art, Cantidad = cant,
                Precio = 10m, PrecioBruto = 10m, PrctjeDesc = 0m, CodigoImpuesto = "IVA", Impuesto = cant * 1.2m, CodAlmacen = "01", FactorUom = factor, UomEntry = uom, BaseTipo = -1 });

        private EntregaCompra Ent(int entry, int numDoc, string sn = "P1", string? estadoDoc = "A", string? cancelado = "N")
        {
            var e = new EntregaCompra { Entry = entry, NumDoc = numDoc, CodigoSn = sn, NombreSn = "Proveedor", Direccion = "Dir", Nit = "1234567-8",
                MonedaDoc = "QTZ", PrctjeDesc = 0m, PrctjeImpuesto = 12m, Comentario = "nota", EstadoDoc = estadoDoc, Cancelado = cancelado };
            _entregas.Add(e);
            return e;
        }

        private void LineaEnt(int entry, int noLinea, string art, decimal cant, int? baseTipo = null, int? baseEntry = null, int? baseLinea = null) =>
            _entLineas.Add(new EntregaCompraDetalle { Entry = entry, NoLinea = noLinea, CodArticulo = art, Cantidad = cant, Precio = 10m,
                BaseTipo = baseTipo ?? -1, BaseEntry = baseEntry, BaseLinea = baseLinea });

        private FacturaCompra Fac(int entry, int numDoc, string sn = "P1", string? estadoDoc = "A", string? cancelado = "N", string subTipo = "--")
        {
            var f = new FacturaCompra { Entry = entry, NumDoc = numDoc, CodigoSn = sn, NombreSn = "Proveedor", Direccion = "Dir", Nit = "1234567-8",
                MonedaDoc = "QTZ", PrctjeDesc = 0m, PrctjeImpuesto = 12m, Comentario = "nota", EstadoDoc = estadoDoc, Cancelado = cancelado, DocSubType = subTipo };
            _facturas.Add(f);
            return f;
        }

        private void LineaFac(int entry, int noLinea, string art, decimal cant, int? baseTipo = null, int? baseEntry = null, int? baseLinea = null) =>
            _facLineas.Add(new FacturaCompraDetalle { Entry = entry, NoLinea = noLinea, CodArticulo = art, Cantidad = cant, Precio = 10m,
                BaseTipo = baseTipo ?? -1, BaseEntry = baseEntry, BaseLinea = baseLinea });

        private void PedidoBase()
        {
            Ped(1, 5);
            LineaPed(1, 1, "A1", 10m);
        }

        private static LineaBaseRequest Req(int tipo, int entry, int linea, string art, decimal cant) => new(tipo, entry, linea, art, cant);

        // ---------- ObtenerBorradorAsync ----------

        [Fact]
        public async Task B1_Borrador_SinConsumo_TraeLineaCompletaYCabecera()
        {
            PedidoBase();

            var b = await _svc.ObtenerBorradorAsync(11, 1, 12);

            var l = Assert.Single(b.Lineas);
            Assert.Equal(10m, l.CantidadOrigen);
            Assert.Equal(10m, l.CantidadAbierta);
            Assert.Equal(11, l.BaseTipo);
            Assert.Equal(1, l.BaseEntry);
            Assert.Equal(1, l.BaseLinea);
            Assert.Equal(10m, l.CantidadBase);
            Assert.Equal(5, b.OrigenNumDoc);
            Assert.Equal("P1", b.Cabecera.CodigoSn);
            Assert.Equal("1234567-8", b.Cabecera.Nit);
            Assert.Equal("QTZ", b.Cabecera.MonedaDoc);
        }

        [Fact]
        public async Task B2_Borrador_ConEntregaParcial_DescuentaLoCopiado()
        {
            PedidoBase();
            Ent(2, 7);
            LineaEnt(2, 1, "A1", 4m, 11, 1, 1);

            var b = await _svc.ObtenerBorradorAsync(11, 1, 13);

            Assert.Equal(6m, Assert.Single(b.Lineas).CantidadAbierta);
        }

        [Fact]
        public async Task B3_Borrador_TodoConsumidoEntreEntregaYFactura_Lanza()
        {
            PedidoBase();
            Ent(2, 7);
            LineaEnt(2, 1, "A1", 4m, 11, 1, 1);
            Fac(3, 9);
            LineaFac(3, 1, "A1", 6m, 11, 1, 1);

            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ObtenerBorradorAsync(11, 1, 13));

            Assert.Equal("El documento no tiene líneas abiertas.", ex.Message);
        }

        [Fact]
        public async Task B4_Borrador_EntregaCancelada_LiberaLaCantidad()
        {
            PedidoBase();
            Ent(2, 7, cancelado: "S");
            LineaEnt(2, 1, "A1", 4m, 11, 1, 1);

            var b = await _svc.ObtenerBorradorAsync(11, 1, 13);

            Assert.Equal(10m, Assert.Single(b.Lineas).CantidadAbierta);
        }

        [Fact]
        public async Task B5_Borrador_FacturaCancelada_NoConsume()
        {
            PedidoBase();
            Fac(3, 9, cancelado: "S");
            LineaFac(3, 1, "A1", 6m, 11, 1, 1);

            var b = await _svc.ObtenerBorradorAsync(11, 1, 12);

            Assert.Equal(10m, Assert.Single(b.Lineas).CantidadAbierta);
        }

        [Fact]
        public async Task B6_Borrador_EntregaConEstadoDocCerrado_SiConsume()
        {
            PedidoBase();
            Ent(2, 7, estadoDoc: "C", cancelado: "N");
            LineaEnt(2, 1, "A1", 4m, 11, 1, 1);

            var b = await _svc.ObtenerBorradorAsync(11, 1, 13);

            Assert.Equal(6m, Assert.Single(b.Lineas).CantidadAbierta);
        }

        [Fact]
        public async Task B7_Borrador_NotaDeDebito_NoConsume()
        {
            PedidoBase();
            Fac(3, 9, subTipo: "DN");
            LineaFac(3, 1, "A1", 6m, 11, 1, 1);

            var b = await _svc.ObtenerBorradorAsync(11, 1, 12);

            Assert.Equal(10m, Assert.Single(b.Lineas).CantidadAbierta);
        }

        [Fact]
        public async Task B8_Borrador_OrigenCancelado_Lanza()
        {
            Ped(1, 5, cancelado: "S");
            LineaPed(1, 1, "A1", 10m);

            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ObtenerBorradorAsync(11, 1, 12));

            Assert.Equal("El documento origen está cancelado.", ex.Message);
        }

        [Fact]
        public async Task B9_Borrador_OrigenCerrado_Lanza()
        {
            Ped(1, 5, estadoDoc: "C");
            LineaPed(1, 1, "A1", 10m);

            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ObtenerBorradorAsync(11, 1, 12));

            Assert.Equal("El documento origen está cancelado.", ex.Message);
        }

        [Fact]
        public async Task B10_Borrador_OrigenInexistente_Lanza()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ObtenerBorradorAsync(11, 99, 12));

            Assert.Equal(DocumentoBaseMensajes.OrigenNoExiste, ex.Message);
        }

        [Fact]
        public async Task B11_Borrador_CombinacionInvalida_Lanza()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ObtenerBorradorAsync(12, 1, 12));

            Assert.Equal("No se puede copiar Entrega de compra a Entrega de compra.", ex.Message);
        }

        [Fact]
        public async Task B12_Borrador_ConUnidadDeMedida_CalculaCantidadBase()
        {
            Ped(1, 5);
            LineaPed(1, 1, "A1", 3m, factor: 12m, uom: 5);
            Ent(2, 7);
            LineaEnt(2, 1, "A1", 1m, 11, 1, 1);

            var b = await _svc.ObtenerBorradorAsync(11, 1, 13);

            var l = Assert.Single(b.Lineas);
            Assert.Equal(2m, l.CantidadAbierta);
            Assert.Equal(12m, l.FactorUom);
            Assert.Equal(24m, l.CantidadBase);
        }

        [Fact]
        public async Task B13_Borrador_LineaAgotada_NoAparece()
        {
            Ped(1, 5);
            LineaPed(1, 1, "A1", 10m);
            LineaPed(1, 2, "A2", 5m);
            Ent(2, 7);
            LineaEnt(2, 1, "A1", 10m, 11, 1, 1);

            var b = await _svc.ObtenerBorradorAsync(11, 1, 13);

            var l = Assert.Single(b.Lineas);
            Assert.Equal(2, l.BaseLinea);
            Assert.Equal(5m, l.CantidadAbierta);
        }

        [Fact]
        public async Task B14_Borrador_EntregaComoOrigen_DescuentaLaFactura()
        {
            Ent(2, 7);
            LineaEnt(2, 1, "A1", 8m);
            Fac(3, 9);
            LineaFac(3, 1, "A1", 3m, 12, 2, 1);

            var b = await _svc.ObtenerBorradorAsync(12, 2, 13);

            var l = Assert.Single(b.Lineas);
            Assert.Equal(5m, l.CantidadAbierta);
            Assert.Equal(12, l.BaseTipo);
        }

        // ---------- ValidarLineasAsync ----------

        [Fact]
        public async Task V1_Validar_CantidadDentroDeLoAbierto_NoLanza()
        {
            PedidoBase();

            await _svc.ValidarLineasAsync(12, "P1", new[] { Req(11, 1, 1, "A1", 6m) });
        }

        [Fact]
        public async Task V2_Validar_ProveedorDistinto_Lanza()
        {
            PedidoBase();

            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ValidarLineasAsync(12, "P2", new[] { Req(11, 1, 1, "A1", 6m) }));

            Assert.Equal("El proveedor del documento debe ser el del documento base Pedido de compra 5 (P1).", ex.Message);
        }

        [Fact]
        public async Task V3_Validar_OrigenInexistente_Lanza()
        {
            PedidoBase();

            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ValidarLineasAsync(12, "P1", new[] { Req(11, 99, 1, "A1", 6m) }));

            Assert.Equal("El documento base Pedido de compra 99 no existe.", ex.Message);
        }

        [Fact]
        public async Task V4_Validar_OrigenCancelado_Lanza()
        {
            Ped(1, 5, cancelado: "S");
            LineaPed(1, 1, "A1", 10m);

            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ValidarLineasAsync(12, "P1", new[] { Req(11, 1, 1, "A1", 6m) }));

            Assert.Equal("El documento base Pedido de compra 5 está cancelado.", ex.Message);
        }

        [Fact]
        public async Task V5_Validar_LineaInexistente_Lanza()
        {
            PedidoBase();

            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ValidarLineasAsync(12, "P1", new[] { Req(11, 1, 9, "A1", 6m) }));

            Assert.Equal("La línea 9 de Pedido de compra 5 no existe.", ex.Message);
        }

        [Fact]
        public async Task V6_Validar_ArticuloDistinto_Lanza()
        {
            PedidoBase();

            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ValidarLineasAsync(12, "P1", new[] { Req(11, 1, 1, "A2", 6m) }));

            Assert.Equal("El artículo de la línea 1 de Pedido de compra 5 no se puede cambiar.", ex.Message);
        }

        [Fact]
        public async Task V7_Validar_CantidadCero_Lanza()
        {
            PedidoBase();

            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ValidarLineasAsync(12, "P1", new[] { Req(11, 1, 1, "A1", 0m) }));

            Assert.Equal("La cantidad de la línea 1 de Pedido de compra 5 debe ser mayor que cero.", ex.Message);
        }

        [Fact]
        public async Task V8_Validar_ExcedeLoAbierto_Lanza()
        {
            PedidoBase();
            Ent(2, 7);
            LineaEnt(2, 1, "A1", 4m, 11, 1, 1);

            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ValidarLineasAsync(13, "P1", new[] { Req(11, 1, 1, "A1", 7m) }));

            Assert.Equal("La cantidad de la línea 1 de Pedido de compra 5 excede la cantidad abierta (6).", ex.Message);
        }

        [Fact]
        public async Task V9_Validar_ExactamenteLoAbierto_NoLanza()
        {
            PedidoBase();
            Ent(2, 7);
            LineaEnt(2, 1, "A1", 4m, 11, 1, 1);

            await _svc.ValidarLineasAsync(13, "P1", new[] { Req(11, 1, 1, "A1", 6m) });
        }

        [Fact]
        public async Task V10_Validar_DosLineasContraLaMismaLinea_SumanYExceden()
        {
            PedidoBase();
            Ent(2, 7);
            LineaEnt(2, 1, "A1", 4m, 11, 1, 1);

            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ValidarLineasAsync(13, "P1",
                new[] { Req(11, 1, 1, "A1", 3m), Req(11, 1, 1, "A1", 4m) }));

            Assert.Equal("La cantidad de la línea 1 de Pedido de compra 5 excede la cantidad abierta (6).", ex.Message);
        }

        [Fact]
        public async Task V11_Validar_CombinacionInvalida_Lanza()
        {
            Ent(2, 7);
            LineaEnt(2, 1, "A1", 8m);

            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ValidarLineasAsync(12, "P1", new[] { Req(12, 2, 1, "A1", 1m) }));

            Assert.Equal("No se puede copiar Entrega de compra a Entrega de compra.", ex.Message);
        }

        [Fact]
        public async Task V12_Validar_ListaVacia_NoHaceNada()
        {
            await _svc.ValidarLineasAsync(12, "P1", Array.Empty<LineaBaseRequest>());
        }

        // ---------- integridad del origen ----------

        private void PedidoConEntregaYFactura()
        {
            PedidoBase();
            Ent(3, 3);
            LineaEnt(3, 1, "A1", 4m, 11, 1, 1);
            Fac(2, 2);
            LineaFac(2, 1, "A1", 3m, 11, 1, 1);
        }

        [Fact]
        public async Task I1_Derivados_ListaEntregaYFacturaOrdenadasPorTipoYEntry()
        {
            PedidoConEntregaYFactura();

            var r = await _svc.ObtenerDerivadosAsync(11, 1, null);

            Assert.Equal(new[] { "Entrega de compra 3", "Factura de compra 2" }, r.Select(d => d.Etiqueta));
            Assert.All(r, d => Assert.Equal(1, d.BaseLinea));
        }

        [Fact]
        public async Task I2_ValidarOrigenModificable_ConDerivados_Lanza()
        {
            PedidoConEntregaYFactura();

            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ValidarOrigenModificableAsync(11, 1, "cancelar el documento"));

            Assert.Equal("No se puede cancelar el documento: lo usan Entrega de compra 3 y Factura de compra 2. Cancele primero esos documentos.", ex.Message);
        }

        [Fact]
        public async Task I3_ValidarOrigenModificable_SinDerivados_NoLanza()
        {
            PedidoBase();

            await _svc.ValidarOrigenModificableAsync(11, 1, "cancelar el documento");
        }

        [Fact]
        public async Task I4_ValidarLineaModificable_LineaCopiada_Lanza()
        {
            PedidoBase();
            Ent(3, 3);
            LineaEnt(3, 1, "A1", 4m, 11, 1, 1);

            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ValidarLineaModificableAsync(11, 1, 1, "modificar"));

            Assert.Equal("No se puede modificar la línea 1: la usan Entrega de compra 3. Cancele primero esos documentos.", ex.Message);
        }

        [Fact]
        public async Task I5_ValidarLineaModificable_OtraLinea_NoLanza()
        {
            PedidoBase();
            LineaPed(1, 2, "A2", 5m);
            Ent(3, 3);
            LineaEnt(3, 1, "A1", 4m, 11, 1, 1);

            await _svc.ValidarLineaModificableAsync(11, 1, 2, "eliminar");
        }

        [Fact]
        public async Task I6_Derivado_Cancelado_NoBloquea()
        {
            PedidoBase();
            Ent(3, 3, cancelado: "S");
            LineaEnt(3, 1, "A1", 4m, 11, 1, 1);

            await _svc.ValidarOrigenModificableAsync(11, 1, "eliminar el documento");
            await _svc.ValidarLineaModificableAsync(11, 1, 1, "modificar");
            Assert.Empty(await _svc.ObtenerDerivadosAsync(11, 1, null));
        }
    }
}
