using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    // Valida el mapeo EF de condiciones de pago y vencimientos sin tocar la base de datos (el modelo se construye en memoria).
    public class ModeloCondicionesPagoTests
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
        public void CondicionPago_MapeaTablaClaveLongitudesYDefaults()
        {
            var et = Modelo().FindEntityType(typeof(CondicionPago))!;
            Assert.Equal("CondicionPago", et.GetTableName());
            Assert.Equal(new[] { "Codigo" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
            Assert.Equal(10, et.FindProperty("Codigo")!.GetMaxLength());
            Assert.Equal(50, et.FindProperty("Nombre")!.GetMaxLength());
            Assert.Equal(typeof(int), et.FindProperty("Dias")!.ClrType);
            Assert.Equal(1, et.FindProperty("Activo")!.GetMaxLength());
            Assert.Equal("S", et.FindProperty("Activo")!.GetDefaultValueSql()!.Trim('(', ')', '\''));
        }

        [Theory]
        [InlineData(typeof(SocioNegocio), false)]
        [InlineData(typeof(Factura), true)]
        [InlineData(typeof(FacturaCompra), true)]
        public void TablasConCondicionPago_TienenColumnasYRelacionConCondicionPago(Type entidad, bool conVencimiento)
        {
            var et = Modelo().FindEntityType(entidad)!;
            var condicion = et.FindProperty("CondicionPago")!;
            Assert.Equal(typeof(string), condicion.ClrType);
            Assert.True(condicion.IsNullable);
            Assert.Equal(10, condicion.GetMaxLength());

            var fk = et.GetForeignKeys().Single(f => f.Properties.Single().Name == "CondicionPago");
            Assert.Equal(typeof(CondicionPago), fk.PrincipalEntityType.ClrType);
            Assert.Equal("CondicionPagoNavigation", fk.DependentToPrincipal!.Name);
            Assert.Null(fk.PrincipalToDependent);

            var vencimiento = et.FindProperty("FechaVencimiento");
            if (conVencimiento)
            {
                Assert.NotNull(vencimiento);
                Assert.Equal(typeof(DateTime?), vencimiento!.ClrType);
                Assert.Equal("datetime", vencimiento.GetColumnType());
            }
            else
            {
                Assert.Null(vencimiento);
            }
        }
    }
}
