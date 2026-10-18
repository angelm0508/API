using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class DocumentoBaseServiceTests
    {
        private readonly Mock<IRepositorioGenerico<Cotizacion, int>> _cot = new();
        private readonly Mock<IRepositorioGenerico<CotizacionDetalle, (int Entry, int NoLinea)>> _cotDet = new();
        private readonly Mock<IRepositorioGenerico<Pedido, int>> _ped = new();
        private readonly Mock<IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)>> _pedDet = new();
        private readonly Mock<IRepositorioGenerico<Entrega, int>> _ent = new();
        private readonly Mock<IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)>> _entDet = new();
        private readonly Mock<IRepositorioGenerico<Factura, int>> _fac = new();
        private readonly Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>> _facDet = new();

        private readonly List<Cotizacion> _cotizaciones = new();
        private readonly List<CotizacionDetalle> _cotLineas = new();
        private readonly List<Pedido> _pedidos = new();
        private readonly List<PedidoDetalle> _pedLineas = new();
        private readonly List<Entrega> _entregas = new();
        private readonly List<EntregaDetalle> _entLineas = new();
        private readonly List<Factura> _facturas = new();
        private readonly List<FacturaDetalle> _facLineas = new();

        private readonly DocumentoBaseService _svc;

        public DocumentoBaseServiceTests()
        {
            _cot.Setup(r => r.ObtenerAsync(It.IsAny<int>())).ReturnsAsync((int id) => _cotizaciones.FirstOrDefault(x => x.Entry == id)!);
            _ped.Setup(r => r.ObtenerAsync(It.IsAny<int>())).ReturnsAsync((int id) => _pedidos.FirstOrDefault(x => x.Entry == id)!);
            _ent.Setup(r => r.ObtenerAsync(It.IsAny<int>())).ReturnsAsync((int id) => _entregas.FirstOrDefault(x => x.Entry == id)!);
            _fac.Setup(r => r.ObtenerAsync(It.IsAny<int>())).ReturnsAsync((int id) => _facturas.FirstOrDefault(x => x.Entry == id)!);
            _cotDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _cotLineas.AsAsyncQueryable());
            _pedDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _pedLineas.AsAsyncQueryable());
            _entDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _entLineas.AsAsyncQueryable());
            _facDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _facLineas.AsAsyncQueryable());

            _svc = new DocumentoBaseService(_cot.Object, _cotDet.Object, _ped.Object, _pedDet.Object,
                _ent.Object, _entDet.Object, _fac.Object, _facDet.Object);
        }

        // ---------- fábricas de datos ----------

        private Cotizacion Cot(int entry, int numDoc, string sn = "CL-1", string? estadoDoc = "A", string? cancelado = "N")
        {
            var c = new Cotizacion { Entry = entry, NumDoc = numDoc, CodigoSn = sn, NombreSn = "Cliente", Direccion = "Dir", MonedaDoc = "QTZ",
                PrctjeDesc = 0m, PrctjeImpuesto = 12m, Comentario = "nota", EstadoDoc = estadoDoc, Cancelado = cancelado };
            _cotizaciones.Add(c);
            return c;
        }

        private void LineaCot(int entry, int noLinea, string art, decimal cant, decimal precio = 10m, string? alm = "01") =>
            _cotLineas.Add(new CotizacionDetalle { Entry = entry, NoLinea = noLinea, CodArticulo = art, Descripcion = "Desc " + art,
                Cantidad = cant, Precio = precio, PrecioBruto = precio, PrctjeDesc = 0m, CodigoImpuesto = "IVA", Impuesto = cant * precio * 0.12m, CodAlmacen = alm });

        private Pedido Ped(int entry, int numDoc, string sn = "CL-1", string? estadoDoc = "A", string? cancelado = "N")
        {
            var p = new Pedido { Entry = entry, NumDoc = numDoc, CodigoSn = sn, EstadoDoc = estadoDoc, Cancelado = cancelado };
            _pedidos.Add(p);
            return p;
        }

        private void LineaPed(int entry, int noLinea, string art, decimal cant, int? baseTipo = null, int? baseEntry = null, int? baseLinea = null) =>
            _pedLineas.Add(new PedidoDetalle { Entry = entry, NoLinea = noLinea, CodArticulo = art, Cantidad = cant, Precio = 10m,
                BaseTipo = baseTipo ?? -1, BaseEntry = baseEntry, BaseLinea = baseLinea });

        private Entrega Ent(int entry, int numDoc, string sn = "CL-1", string? estadoDoc = "A", string? cancelado = "N")
        {
            var e = new Entrega { Entry = entry, NumDoc = numDoc, CodigoSn = sn, EstadoDoc = estadoDoc, Cancelado = cancelado };
            _entregas.Add(e);
            return e;
        }

        private void LineaEnt(int entry, int noLinea, string art, decimal cant, int? baseTipo = null, int? baseEntry = null, int? baseLinea = null) =>
            _entLineas.Add(new EntregaDetalle { Entry = entry, NoLinea = noLinea, CodArticulo = art, Cantidad = cant, Precio = 10m,
                BaseTipo = baseTipo ?? -1, BaseEntry = baseEntry, BaseLinea = baseLinea });

        private Factura Fac(int entry, int numDoc, string sn = "CL-1", string? estadoDoc = "A", string? cancelado = "N", string subTipo = "--")
        {
            var f = new Factura { Entry = entry, NumDoc = numDoc, CodigoSn = sn, EstadoDoc = estadoDoc, Cancelado = cancelado, DocSubType = subTipo };
            _facturas.Add(f);
            return f;
        }

        private void LineaFac(int entry, int noLinea, string art, decimal cant, int? baseTipo = null, int? baseEntry = null, int? baseLinea = null) =>
            _facLineas.Add(new FacturaDetalle { Entry = entry, NoLinea = noLinea, CodArticulo = art, Cantidad = cant, Precio = 10m,
                BaseTipo = baseTipo ?? -1, BaseEntry = baseEntry, BaseLinea = baseLinea });

        // ---------- ObtenerDerivadosAsync ----------

        [Fact]
        public async Task ObtenerDerivados_SinConsumidores_DevuelveVacio()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);

            var r = await _svc.ObtenerDerivadosAsync(TipoDocumentoVenta.Cotizacion, 1);

            Assert.Empty(r);
        }

        [Fact]
        public async Task ObtenerDerivados_PedidoYFacturaActivos_LosListaOrdenadosPorTipo()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            Fac(40, 2);
            LineaFac(40, 1, "ART1", 3m, baseTipo: 3, baseEntry: 1, baseLinea: 1);
            Ped(30, 3);
            LineaPed(30, 1, "ART1", 4m, baseTipo: 3, baseEntry: 1, baseLinea: 1);

            var r = await _svc.ObtenerDerivadosAsync(TipoDocumentoVenta.Cotizacion, 1);

            Assert.Equal(new[] { "Pedido 3", "Factura 2" }, r.Select(d => d.Etiqueta));
        }

        [Fact]
        public async Task ObtenerDerivados_IgnoraCanceladosYNotasDeDebito()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            Ped(30, 3, estadoDoc: "C");                          // pedido cancelado por EstadoDoc
            LineaPed(30, 1, "ART1", 4m, 3, 1, 1);
            Ent(31, 4, cancelado: "S");                          // entrega cancelada por Cancelado
            LineaEnt(31, 1, "ART1", 2m, 3, 1, 1);
            Fac(40, 9, subTipo: "ND");                           // nota de débito: no es factura
            LineaFac(40, 1, "ART1", 1m, 3, 1, 1);

            var r = await _svc.ObtenerDerivadosAsync(TipoDocumentoVenta.Cotizacion, 1);

            Assert.Empty(r);
        }

        [Fact]
        public async Task ObtenerDerivados_PorLinea_SoloLosQueUsanEsaLinea()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            LineaCot(1, 2, "ART2", 10m);
            Ped(30, 3);
            LineaPed(30, 1, "ART1", 4m, 3, 1, 1);
            Ped(31, 5);
            LineaPed(31, 1, "ART2", 4m, 3, 1, 2);

            var r = await _svc.ObtenerDerivadosAsync(TipoDocumentoVenta.Cotizacion, 1, noLinea: 2);

            Assert.Equal(new[] { "Pedido 5" }, r.Select(d => d.Etiqueta));
        }

        [Fact]
        public async Task ObtenerDerivados_IgnoraLineasDeOtroOrigenYLasDeCantidadCero()
        {
            Cot(1, 12);
            Cot(2, 13);
            LineaCot(1, 1, "ART1", 10m);
            Ped(30, 3);
            LineaPed(30, 1, "ART1", 4m, 3, 2, 1);   // base: otra cotización
            Ped(31, 4);
            LineaPed(31, 1, "ART1", 0m, 3, 1, 1);   // cantidad cero: no consume

            var r = await _svc.ObtenerDerivadosAsync(TipoDocumentoVenta.Cotizacion, 1);

            Assert.Empty(r);
        }

        [Fact]
        public async Task ObtenerDerivados_OrigenFactura_NoTieneDestinos()
        {
            Fac(40, 2);

            Assert.Empty(await _svc.ObtenerDerivadosAsync(TipoDocumentoVenta.Factura, 40));
        }

        // ---------- ObtenerBorradorAsync ----------

        [Fact]
        public async Task Borrador_CopiaCabeceraYLineasAbiertas_ConCantidadAbierta()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m, precio: 25m);
            LineaCot(1, 2, "ART2", 5m, precio: 8m, alm: null);
            Ped(30, 3);
            LineaPed(30, 1, "ART1", 4m, baseTipo: 3, baseEntry: 1, baseLinea: 1);   // consume 4 de las 10

            var b = await _svc.ObtenerBorradorAsync(TipoDocumentoVenta.Cotizacion, 1, TipoDocumentoVenta.Factura);

            Assert.Equal((3, 1, 12), (b.OrigenTipo, b.OrigenEntry, b.OrigenNumDoc));
            Assert.Equal(("CL-1", "Cliente", "Dir", "QTZ", 12m, "nota"),
                (b.Cabecera.CodigoSn, b.Cabecera.NombreSn, b.Cabecera.Direccion, b.Cabecera.MonedaDoc, b.Cabecera.PrctjeImpuesto, b.Cabecera.Comentario));
            Assert.Equal(2, b.Lineas.Count);
            Assert.Equal((3, 1, 1, "ART1", 10m, 6m, 25m, "01"),
                (b.Lineas[0].BaseTipo, b.Lineas[0].BaseEntry, b.Lineas[0].BaseLinea, b.Lineas[0].CodArticulo,
                 b.Lineas[0].CantidadOrigen, b.Lineas[0].CantidadAbierta, b.Lineas[0].Precio, b.Lineas[0].CodAlmacen));
            Assert.Equal(("ART2", 5m, 5m), (b.Lineas[1].CodArticulo, b.Lineas[1].CantidadOrigen, b.Lineas[1].CantidadAbierta));
            Assert.Null(b.Lineas[1].CodAlmacen);
        }

        [Fact]
        public async Task Borrador_OmiteLasLineasAgotadas()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            LineaCot(1, 2, "ART2", 5m);
            Ped(30, 3);
            LineaPed(30, 1, "ART1", 10m, 3, 1, 1);   // agota la línea 1

            var b = await _svc.ObtenerBorradorAsync(TipoDocumentoVenta.Cotizacion, 1, TipoDocumentoVenta.Pedido);

            Assert.Equal(new[] { "ART2" }, b.Lineas.Select(l => l.CodArticulo));
        }

        [Fact]
        public async Task Borrador_UnDestinoCanceladoLiberaLaCantidad()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            Ped(30, 3, estadoDoc: "C");
            LineaPed(30, 1, "ART1", 10m, 3, 1, 1);

            var b = await _svc.ObtenerBorradorAsync(TipoDocumentoVenta.Cotizacion, 1, TipoDocumentoVenta.Entrega);

            Assert.Equal(10m, b.Lineas.Single().CantidadAbierta);
        }

        [Fact]
        public async Task Borrador_SumaLosConsumosDeVariosDestinos()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            Ped(30, 3);
            LineaPed(30, 1, "ART1", 2m, 3, 1, 1);
            Fac(40, 7);
            LineaFac(40, 1, "ART1", 3m, 3, 1, 1);

            var b = await _svc.ObtenerBorradorAsync(TipoDocumentoVenta.Cotizacion, 1, TipoDocumentoVenta.Entrega);

            Assert.Equal(5m, b.Lineas.Single().CantidadAbierta);
        }

        [Theory]
        [InlineData(4, 3)]    // Pedido -> Cotización (hacia atrás)
        [InlineData(5, 4)]
        [InlineData(6, 6)]
        public async Task Borrador_CombinacionInvalida_Lanza(int origen, int destino)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ObtenerBorradorAsync(origen, 1, destino));
            Assert.Equal($"No se puede copiar {TipoDocumentoVenta.Nombre(origen)} a {TipoDocumentoVenta.Nombre(destino)}.", ex.Message);
        }

        [Fact]
        public async Task Borrador_OrigenInexistente_Lanza()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ObtenerBorradorAsync(TipoDocumentoVenta.Cotizacion, 99, TipoDocumentoVenta.Pedido));
            Assert.Equal(DocumentoBaseMensajes.OrigenNoExiste, ex.Message);
        }

        [Theory]
        [InlineData("C", "N")]
        [InlineData("A", "S")]
        public async Task Borrador_OrigenInactivo_Lanza(string estadoDoc, string cancelado)
        {
            Cot(1, 12, estadoDoc: estadoDoc, cancelado: cancelado);
            LineaCot(1, 1, "ART1", 10m);

            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ObtenerBorradorAsync(TipoDocumentoVenta.Cotizacion, 1, TipoDocumentoVenta.Pedido));
            Assert.Equal("El documento origen está cancelado.", ex.Message);
        }

        [Fact]
        public async Task Borrador_SinLineasAbiertas_Lanza()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            Ped(30, 3);
            LineaPed(30, 1, "ART1", 10m, 3, 1, 1);

            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ObtenerBorradorAsync(TipoDocumentoVenta.Cotizacion, 1, TipoDocumentoVenta.Pedido));
            Assert.Equal("El documento no tiene líneas abiertas.", ex.Message);
        }

        // ---------- ValidarLineasAsync ----------

        private static LineaBaseRequest Req(int baseTipo, int baseEntry, int baseLinea, string art, decimal cant) =>
            new(baseTipo, baseEntry, baseLinea, art, cant);

        [Fact]
        public async Task Validar_SinLineas_NoHaceNadaNiLeeNada()
        {
            await _svc.ValidarLineasAsync(TipoDocumentoVenta.Factura, "CL-1", Array.Empty<LineaBaseRequest>());

            _cot.Verify(r => r.ObtenerAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Validar_LineasCorrectasConCantidadParcial_Pasa()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);

            await _svc.ValidarLineasAsync(TipoDocumentoVenta.Pedido, "CL-1", new[] { Req(3, 1, 1, "ART1", 10m) });
            await _svc.ValidarLineasAsync(TipoDocumentoVenta.Entrega, "cl-1", new[] { Req(3, 1, 1, "ART1", 0.5m) });   // socio sin distinguir mayúsculas
        }

        [Fact]
        public async Task Validar_CombinacionInvalida_Lanza()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Pedido, "CL-1", new[] { Req(5, 1, 1, "ART1", 1m) }));
            Assert.Equal("No se puede copiar Entrega a Pedido.", ex.Message);
        }

        [Fact]
        public async Task Validar_OrigenInexistente_Lanza()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Pedido, "CL-1", new[] { Req(3, 99, 1, "ART1", 1m) }));
            Assert.Equal("El documento base Cotización 99 no existe.", ex.Message);
        }

        [Fact]
        public async Task Validar_OrigenCancelado_Lanza()
        {
            Cot(1, 12, estadoDoc: "C");
            LineaCot(1, 1, "ART1", 10m);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Pedido, "CL-1", new[] { Req(3, 1, 1, "ART1", 1m) }));
            Assert.Equal("El documento base Cotización 12 está cancelado.", ex.Message);
        }

        [Fact]
        public async Task Validar_SocioDistinto_Lanza()
        {
            Cot(1, 12, sn: "CL-1");
            LineaCot(1, 1, "ART1", 10m);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Pedido, "CL-2", new[] { Req(3, 1, 1, "ART1", 1m) }));
            Assert.Equal("El socio del documento debe ser el del documento base Cotización 12 (CL-1).", ex.Message);
        }

        [Fact]
        public async Task Validar_LineaOrigenInexistente_Lanza()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Pedido, "CL-1", new[] { Req(3, 1, 7, "ART1", 1m) }));
            Assert.Equal("La línea 7 de Cotización 12 no existe.", ex.Message);
        }

        [Fact]
        public async Task Validar_ArticuloDistinto_Lanza()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Pedido, "CL-1", new[] { Req(3, 1, 1, "OTRO", 1m) }));
            Assert.Equal("El artículo de la línea 1 de Cotización 12 no se puede cambiar.", ex.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-2)]
        public async Task Validar_CantidadNoPositiva_Lanza(double cantidad)
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Pedido, "CL-1", new[] { Req(3, 1, 1, "ART1", (decimal)cantidad) }));
            Assert.Equal("La cantidad de la línea 1 de Cotización 12 debe ser mayor que cero.", ex.Message);
        }

        [Fact]
        public async Task Validar_CantidadMayorQueLaAbierta_Lanza()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            Ped(30, 3);
            LineaPed(30, 1, "ART1", 4m, 3, 1, 1);   // abierta = 6

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Entrega, "CL-1", new[] { Req(3, 1, 1, "ART1", 6.5m) }));
            Assert.Equal("La cantidad de la línea 1 de Cotización 12 excede la cantidad abierta (6).", ex.Message);
        }

        [Fact]
        public async Task Validar_VariasLineasContraLaMismaLineaOrigen_SeSuman()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Pedido, "CL-1", new[] { Req(3, 1, 1, "ART1", 6m), Req(3, 1, 1, "ART1", 5m) }));
            Assert.Equal("La cantidad de la línea 1 de Cotización 12 excede la cantidad abierta (10).", ex.Message);
        }

        [Fact]
        public async Task Validar_DestinoCanceladoNoCuentaComoConsumo()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            Ent(31, 4, cancelado: "S");
            LineaEnt(31, 1, "ART1", 10m, 3, 1, 1);

            await _svc.ValidarLineasAsync(TipoDocumentoVenta.Factura, "CL-1", new[] { Req(3, 1, 1, "ART1", 10m) });
        }

        [Fact]
        public async Task Validar_EntregaAFactura_ValidaContraLaEntrega()
        {
            Ent(31, 4);
            LineaEnt(31, 1, "ART1", 5m);

            await _svc.ValidarLineasAsync(TipoDocumentoVenta.Factura, "CL-1", new[] { Req(5, 31, 1, "ART1", 5m) });

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Factura, "CL-1", new[] { Req(5, 31, 1, "ART1", 5.01m) }));
            Assert.Equal("La cantidad de la línea 1 de Entrega 4 excede la cantidad abierta (5).", ex.Message);
        }

        // ---------- Integridad del origen ----------

        [Fact]
        public async Task OrigenModificable_SinDerivados_NoLanza()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);

            await _svc.ValidarOrigenModificableAsync(TipoDocumentoVenta.Cotizacion, 1, "cancelar el documento");
        }

        [Fact]
        public async Task OrigenModificable_ConDerivados_NombraLosDocumentos()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            Ped(30, 3);
            LineaPed(30, 1, "ART1", 4m, 3, 1, 1);
            Fac(40, 2);
            LineaFac(40, 1, "ART1", 3m, 3, 1, 1);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarOrigenModificableAsync(TipoDocumentoVenta.Cotizacion, 1, "cancelar el documento"));
            Assert.Equal("No se puede cancelar el documento: lo usan Pedido 3 y Factura 2. Cancele primero esos documentos.", ex.Message);
        }

        [Fact]
        public async Task OrigenModificable_TresDerivados_UsaComasYY()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            Ped(30, 3);
            LineaPed(30, 1, "ART1", 1m, 3, 1, 1);
            Ent(31, 4);
            LineaEnt(31, 1, "ART1", 1m, 3, 1, 1);
            Fac(40, 2);
            LineaFac(40, 1, "ART1", 1m, 3, 1, 1);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarOrigenModificableAsync(TipoDocumentoVenta.Cotizacion, 1, "eliminar el documento"));
            Assert.Equal("No se puede eliminar el documento: lo usan Pedido 3, Entrega 4 y Factura 2. Cancele primero esos documentos.", ex.Message);
        }

        [Fact]
        public async Task OrigenModificable_DerivadoCancelado_NoBloquea()
        {
            Ent(31, 4);
            LineaEnt(31, 1, "ART1", 5m);
            Fac(40, 2, cancelado: "S");
            LineaFac(40, 1, "ART1", 5m, 5, 31, 1);

            await _svc.ValidarOrigenModificableAsync(TipoDocumentoVenta.Entrega, 31, "cancelar el documento");
        }

        [Fact]
        public async Task LineaModificable_SoloBloqueaLaLineaConsumida()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            LineaCot(1, 2, "ART2", 10m);
            Ped(30, 3);
            LineaPed(30, 1, "ART1", 4m, 3, 1, 1);

            await _svc.ValidarLineaModificableAsync(TipoDocumentoVenta.Cotizacion, 1, 2, "modificar");   // la 2 está libre

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineaModificableAsync(TipoDocumentoVenta.Cotizacion, 1, 1, "eliminar"));
            Assert.Equal("No se puede eliminar la línea 1: la usan Pedido 3. Cancele primero esos documentos.", ex.Message);
        }
    }
}
