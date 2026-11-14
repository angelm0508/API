using API.Domain.Core.Reportes.Compartido;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class TiposDocumentoInventarioTests
    {
        [Theory]
        [InlineData("5", "Entrega")]
        [InlineData("6", "Factura / Nota de débito")]
        [InlineData("12", "Entrega de compra")]
        [InlineData("13", "Factura de compra / Nota de débito de compra")]
        [InlineData("15", "Nota de crédito de compra")]
        [InlineData("59", "Entrada de mercancía")]
        [InlineData("60", "Salida de mercancía")]
        [InlineData("70", "Orden de servicio")]
        public void Nombre_DevuelveElNombreDelTipoConocido(string codigo, string esperado)
        {
            Assert.Equal(esperado, TiposDocumentoInventario.Nombre(codigo));
        }

        [Fact]
        public void Nombre_TipoDesconocido_UsaElCodigo()
        {
            Assert.Equal("Documento 99", TiposDocumentoInventario.Nombre("99"));
        }

        [Fact]
        public void Opciones_SonLosDocumentosQueRealmenteMuevenInventario()
        {
            // Solo los dominios que llaman a AsentarAsync generan movimientos: las notas de crédito (8 y 15) no.
            Assert.Equal(new[] { "5", "6", "12", "13", "59", "60", "70" }, TiposDocumentoInventario.Opciones.Select(o => o.Valor).ToArray());
            Assert.All(TiposDocumentoInventario.Opciones, o => Assert.Equal(TiposDocumentoInventario.Nombre(o.Valor), o.Titulo));
        }
    }
}
