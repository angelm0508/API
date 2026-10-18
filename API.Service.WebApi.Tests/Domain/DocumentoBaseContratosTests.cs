using API.Domain.Interface;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class DocumentoBaseContratosTests
    {
        [Theory]
        [InlineData(3, 4, true)]
        [InlineData(3, 5, true)]
        [InlineData(3, 6, true)]
        [InlineData(4, 5, true)]
        [InlineData(4, 6, true)]
        [InlineData(5, 6, true)]
        [InlineData(4, 3, false)]
        [InlineData(5, 4, false)]
        [InlineData(6, 6, false)]
        [InlineData(6, 3, false)]
        [InlineData(3, 3, false)]
        [InlineData(9, 6, false)]
        public void EsCombinacionValida_SoloLaCadenaHaciaAdelante(int origen, int destino, bool esperado)
        {
            Assert.Equal(esperado, TipoDocumentoVenta.EsCombinacionValida(origen, destino));
        }

        [Fact]
        public void DestinosDe_Cotizacion_EsPedidoEntregaFactura()
        {
            Assert.Equal(new[] { 4, 5, 6 }, TipoDocumentoVenta.DestinosDe(TipoDocumentoVenta.Cotizacion));
            Assert.Empty(TipoDocumentoVenta.DestinosDe(TipoDocumentoVenta.Factura));
        }

        [Theory]
        [InlineData(3, "Cotización")]
        [InlineData(4, "Pedido")]
        [InlineData(5, "Entrega")]
        [InlineData(6, "Factura")]
        [InlineData(99, "Documento 99")]
        public void Nombre_DevuelveElNombreEnEspanol(int tipo, string esperado)
        {
            Assert.Equal(esperado, TipoDocumentoVenta.Nombre(tipo));
        }

        [Fact]
        public void DocumentoDerivado_Etiqueta_EsNombreYNumero()
        {
            Assert.Equal("Pedido 3", new DocumentoDerivado(4, 77, 3, 1).Etiqueta);
        }

        [Fact]
        public void Extraer_IgnoraLineasSinBase()
        {
            var lineas = new (int?, int?, int?, string?, decimal?)[]
            {
                (null, null, null, "A", 1m),
                (-1, null, null, "B", 2m),
                (0, 5, 1, "C", 3m),
            };

            Assert.Empty(LineasBase.Extraer(lineas));
        }

        [Fact]
        public void Extraer_ConvierteLasLineasConBase()
        {
            var lineas = new (int?, int?, int?, string?, decimal?)[]
            {
                (-1, null, null, "A", 1m),
                (3, 10, 2, "ART1", 4m),
                (4, 11, 1, "ART2", null),
            };

            var r = LineasBase.Extraer(lineas);

            Assert.Equal(2, r.Count);
            Assert.Equal(new LineaBaseRequest(3, 10, 2, "ART1", 4m), r[0]);
            Assert.Equal(new LineaBaseRequest(4, 11, 1, "ART2", 0m), r[1]);
        }

        [Theory]
        [InlineData(3, null, 1)]
        [InlineData(3, 10, null)]
        public void Extraer_BaseSinEntryOLinea_Lanza(int baseTipo, int? baseEntry, int? baseLinea)
        {
            var lineas = new (int?, int?, int?, string?, decimal?)[] { (baseTipo, baseEntry, baseLinea, "ART1", 1m) };

            var ex = Assert.Throws<Exception>(() => LineasBase.Extraer(lineas));
            Assert.Equal("La línea con documento base debe indicar BaseEntry y BaseLinea.", ex.Message);
        }
    }
}
