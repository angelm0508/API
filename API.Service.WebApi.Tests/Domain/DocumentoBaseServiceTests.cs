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
    }
}
