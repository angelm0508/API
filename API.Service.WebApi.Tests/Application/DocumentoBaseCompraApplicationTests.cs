using API.Application.Main;
using API.Domain.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class DocumentoBaseCompraApplicationTests
    {
        private readonly Mock<IDocumentoBaseCompraService> _servicio = new();
        private readonly DocumentoBaseCompraApplication _app;

        public DocumentoBaseCompraApplicationTests()
        {
            _app = new DocumentoBaseCompraApplication(_servicio.Object);
        }

        [Fact]
        public async Task ObtenerBorrador_MapeaCabeceraYLineas()
        {
            _servicio.Setup(s => s.ObtenerBorradorAsync(11, 10, 12)).ReturnsAsync(new BorradorDocumentoBase(
                11, 10, 12,
                new CabeceraBorrador("PR-1", "Proveedor", "Dir", "QTZ", 5m, 12m, "nota", "1234567-8"),
                new[] { new LineaAbiertaVenta(11, 10, 2, "ART1", "Desc", 10m, 6m, 25m, 25m, 0m, "IVA", 30m, "01", 7, "CJ", 12m, 72m) }));

            var r = await _app.ObtenerBorradorAsync(11, 10, 12);

            Assert.True(r.Resultado);
            Assert.Equal((11, 10, 12), (r.Dato.OrigenTipo, r.Dato.OrigenEntry, r.Dato.OrigenNumDoc));
            Assert.Equal(("PR-1", "Proveedor", "Dir", "QTZ", 5m, 12m, "nota"),
                (r.Dato.Cabecera.CodigoSn, r.Dato.Cabecera.NombreSn, r.Dato.Cabecera.Direccion, r.Dato.Cabecera.MonedaDoc,
                 r.Dato.Cabecera.PrctjeDesc, r.Dato.Cabecera.PrctjeImpuesto, r.Dato.Cabecera.Comentario));
            Assert.Equal("1234567-8", r.Dato.Cabecera.Nit);
            var l = Assert.Single(r.Dato.Lineas);
            Assert.Equal((11, 10, 2, "ART1", "Desc", 10m, 6m, 25m, 25m, 0m, "IVA", 30m, "01"),
                (l.BaseTipo, l.BaseEntry, l.BaseLinea, l.CodArticulo, l.Descripcion, l.CantidadOrigen, l.CantidadAbierta,
                 l.Precio, l.PrecioBruto, l.PrctjeDesc, l.CodigoImpuesto, l.Impuesto, l.CodAlmacen));
            Assert.Equal((7, "CJ", 12m, 72m), (l.UomEntry, l.UomCodigo, l.FactorUom, l.CantidadBase));
        }

        [Fact]
        public async Task ObtenerBorrador_ConError_DevuelveElMensajeDelServicio()
        {
            _servicio.Setup(s => s.ObtenerBorradorAsync(12, 1, 11)).ThrowsAsync(new Exception("No se puede copiar Entrega de compra a Pedido de compra."));

            var r = await _app.ObtenerBorradorAsync(12, 1, 11);

            Assert.False(r.Resultado);
            Assert.Equal("No se puede copiar Entrega de compra a Pedido de compra.", r.Mensaje);
            Assert.Null(r.Dato);
        }
    }
}
