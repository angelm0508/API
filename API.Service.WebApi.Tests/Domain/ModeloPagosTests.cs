using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    // Valida el mapeo EF de las entidades de pagos sin tocar la base de datos (el modelo se construye en memoria).
    public class ModeloPagosTests
    {
        private static readonly DbContextOptions<ApiDbTestContext> _opciones =
            new DbContextOptionsBuilder<ApiDbTestContext>()
                .UseSqlServer("Server=localhost;Database=API_DB_TEST;Trusted_Connection=True;TrustServerCertificate=True")
                .Options;

        private static IModel Modelo()
        {
            using var ctx = new ApiDbTestContext(_opciones);
            return ctx.Model;
        }

        [Fact]
        public void FormaPago_MapeaTablaYClave()
        {
            var et = Modelo().FindEntityType(typeof(FormaPago))!;
            Assert.Equal("FormaPago", et.GetTableName());
            Assert.Equal(new[] { "Codigo" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
        }

        [Fact]
        public void PagoRecibido_MapeaTablaClaveYColumnasDeSocio()
        {
            var et = Modelo().FindEntityType(typeof(PagoRecibido))!;
            Assert.Equal("PagoRecibido", et.GetTableName());
            Assert.Equal(new[] { "Entry" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
            Assert.Equal("CodigoSN", et.FindProperty("CodigoSn")!.GetColumnName());
            Assert.Equal("NombreSN", et.FindProperty("NombreSn")!.GetColumnName());
            Assert.Equal("decimal(19,6)", et.FindProperty("TotalPago")!.GetColumnType());
        }

        [Fact]
        public void PagoRecibidoFactura_ClaveCompuestaYRelacionConFactura()
        {
            var et = Modelo().FindEntityType(typeof(PagoRecibidoFactura))!;
            Assert.Equal("PagoRecibidoFactura", et.GetTableName());
            Assert.Equal(new[] { "Entry", "NoLinea" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
            var fk = et.GetForeignKeys().Single(f => f.Properties.Single().Name == "FacturaEntry");
            Assert.Equal(typeof(Factura), fk.PrincipalEntityType.ClrType);
            Assert.Equal("decimal(19,6)", et.FindProperty("MontoAplicado")!.GetColumnType());
        }

        [Fact]
        public void PagoRecibidoMedio_ClaveCompuestaYRelacionConFormaPago()
        {
            var et = Modelo().FindEntityType(typeof(PagoRecibidoMedio))!;
            Assert.Equal("PagoRecibidoMedio", et.GetTableName());
            Assert.Equal(new[] { "Entry", "NoLinea" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
            var fk = et.GetForeignKeys().Single(f => f.Properties.Single().Name == "CodFormaPago");
            Assert.Equal(typeof(FormaPago), fk.PrincipalEntityType.ClrType);
            Assert.Equal("decimal(19,6)", et.FindProperty("Monto")!.GetColumnType());
        }
    }
}
