using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    /// <summary>
    /// Verifica sin base de datos el mapeo EF de DocSubType/DocType y que el detalle de factura
    /// acepta artículo y almacén nulos (notas de débito por concepto). Debe coincidir con
    /// sql/2026-10-17-notas-debito-en-factura.sql.
    /// </summary>
    public class FacturaModeloTests
    {
        private static Microsoft.EntityFrameworkCore.Metadata.IModel Modelo()
        {
            var opciones = new DbContextOptionsBuilder<ApiDbTestContext>()
                .UseSqlServer("Server=.;Database=x;Trusted_Connection=True;TrustServerCertificate=True")
                .Options;
            using var contexto = new ApiDbTestContext(opciones);
            return contexto.Model;
        }

        [Theory]
        [InlineData(typeof(Factura))]
        [InlineData(typeof(FacturaCompra))]
        public void Encabezado_MapeaDocSubTypeYDocType(Type tipo)
        {
            var entidad = Modelo().FindEntityType(tipo)!;

            var subTipo = entidad.FindProperty("DocSubType")!;
            Assert.Equal(2, subTipo.GetMaxLength());
            Assert.Equal("('--')", subTipo.GetDefaultValueSql());
            Assert.False(subTipo.IsNullable);

            var modo = entidad.FindProperty("DocType")!;
            Assert.Equal(1, modo.GetMaxLength());
            Assert.Equal("('I')", modo.GetDefaultValueSql());
            Assert.False(modo.IsNullable);
        }

        [Theory]
        [InlineData(typeof(FacturaDetalle))]
        [InlineData(typeof(FacturaCompraDetalle))]
        public void Detalle_AceptaArticuloYAlmacenNulos(Type tipo)
        {
            var entidad = Modelo().FindEntityType(tipo)!;

            Assert.True(entidad.FindProperty("CodArticulo")!.IsNullable);
            Assert.True(entidad.FindProperty("CodAlmacen")!.IsNullable);
        }

        [Fact]
        public void LasEntidadesNuevasNacenComoFacturaPorArticulos()
        {
            Assert.Equal("--", new Factura().DocSubType);
            Assert.Equal("I", new Factura().DocType);
            Assert.Equal("--", new FacturaCompra().DocSubType);
            Assert.Equal("I", new FacturaCompra().DocType);
        }
    }
}
