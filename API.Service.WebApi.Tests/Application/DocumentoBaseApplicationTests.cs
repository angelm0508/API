using API.Application.Main;
using API.Domain.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class DocumentoBaseApplicationTests
    {
        private readonly Mock<IDocumentoBaseService> _servicio = new();
        private readonly DocumentoBaseApplication _app;

        public DocumentoBaseApplicationTests()
        {
            _app = new DocumentoBaseApplication(_servicio.Object);
        }

        [Fact]
        public async Task ObtenerBorrador_MapeaCabeceraYLineas()
        {
            _servicio.Setup(s => s.ObtenerBorradorAsync(3, 10, 4)).ReturnsAsync(new BorradorDocumentoBase(
                3, 10, 12,
                new CabeceraBorrador("CL-1", "Cliente", "Dir", "QTZ", 5m, 12m, "nota"),
                new[] { new LineaAbiertaVenta(3, 10, 2, "ART1", "Desc", 10m, 6m, 25m, 25m, 0m, "IVA", 30m, "01") }));

            var r = await _app.ObtenerBorradorAsync(3, 10, 4);

            Assert.True(r.Resultado);
            Assert.Equal((3, 10, 12), (r.Dato.OrigenTipo, r.Dato.OrigenEntry, r.Dato.OrigenNumDoc));
            Assert.Equal(("CL-1", "Cliente", "Dir", "QTZ", 5m, 12m, "nota"),
                (r.Dato.Cabecera.CodigoSn, r.Dato.Cabecera.NombreSn, r.Dato.Cabecera.Direccion, r.Dato.Cabecera.MonedaDoc,
                 r.Dato.Cabecera.PrctjeDesc, r.Dato.Cabecera.PrctjeImpuesto, r.Dato.Cabecera.Comentario));
            var l = Assert.Single(r.Dato.Lineas);
            Assert.Equal((3, 10, 2, "ART1", "Desc", 10m, 6m, 25m, 25m, 0m, "IVA", 30m, "01"),
                (l.BaseTipo, l.BaseEntry, l.BaseLinea, l.CodArticulo, l.Descripcion, l.CantidadOrigen, l.CantidadAbierta,
                 l.Precio, l.PrecioBruto, l.PrctjeDesc, l.CodigoImpuesto, l.Impuesto, l.CodAlmacen));
        }

        [Fact]
        public async Task ObtenerBorrador_ConError_DevuelveElMensajeDelServicio()
        {
            _servicio.Setup(s => s.ObtenerBorradorAsync(4, 1, 3)).ThrowsAsync(new Exception("No se puede copiar Pedido a Cotización."));

            var r = await _app.ObtenerBorradorAsync(4, 1, 3);

            Assert.False(r.Resultado);
            Assert.Equal("No se puede copiar Pedido a Cotización.", r.Mensaje);
            Assert.Null(r.Dato);
        }
    }
}
