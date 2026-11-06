using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    // Valida el mapeo EF del limite de credito y de las autorizaciones sin tocar la base de datos (el modelo se construye en memoria).
    public class ModeloLimiteCreditoTests
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
        public void SocioNegocio_MapeaLimiteCreditoNulableYBloquearSiVencidoConDefaultN()
        {
            var et = Modelo().FindEntityType(typeof(SocioNegocio))!;

            var limite = et.FindProperty("LimiteCredito")!;
            Assert.Equal(typeof(decimal?), limite.ClrType);
            Assert.True(limite.IsNullable);
            Assert.Equal("decimal(19,2)", limite.GetColumnType().Replace(" ", ""));

            var bloquear = et.FindProperty("BloquearSiVencido")!;
            Assert.Equal(typeof(string), bloquear.ClrType);
            Assert.False(bloquear.IsNullable);
            Assert.Equal(1, bloquear.GetMaxLength());
            Assert.Equal("N", bloquear.GetDefaultValueSql()!.Trim('(', ')', '\''));
            Assert.Equal("N", new SocioNegocio().BloquearSiVencido);
        }

        [Fact]
        public void AutorizacionCredito_MapeaTablaClaveLongitudesYTipos()
        {
            var et = Modelo().FindEntityType(typeof(AutorizacionCredito))!;
            Assert.Equal("AutorizacionCredito", et.GetTableName());
            Assert.Equal(new[] { "Id" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
            Assert.Equal(ValueGenerated.OnAdd, et.FindProperty("Id")!.ValueGenerated);

            Assert.Equal(15, et.FindProperty("CodigoSn")!.GetMaxLength());
            Assert.Equal("CodigoSN", et.FindProperty("CodigoSn")!.GetColumnName());
            Assert.Equal(50, et.FindProperty("Usuario")!.GetMaxLength());
            Assert.Equal(20, et.FindProperty("Motivo")!.GetMaxLength());

            Assert.Equal(typeof(int), et.FindProperty("TipoDocumento")!.ClrType);
            Assert.Equal(typeof(int), et.FindProperty("EntryDocumento")!.ClrType);

            var limite = et.FindProperty("Limite")!;
            Assert.Equal(typeof(decimal?), limite.ClrType);
            Assert.True(limite.IsNullable);
            Assert.Equal("decimal(19,2)", limite.GetColumnType().Replace(" ", ""));

            foreach (var nombre in new[] { "Saldo", "Comprometido", "Vencido", "Monto", "Exceso" })
            {
                var p = et.FindProperty(nombre)!;
                Assert.Equal(typeof(decimal), p.ClrType);
                Assert.False(p.IsNullable);
                Assert.Equal("decimal(19,2)", p.GetColumnType().Replace(" ", ""));
            }

            var fecha = et.FindProperty("Fecha")!;
            Assert.Equal(typeof(DateTime), fecha.ClrType);
            Assert.Equal("datetime", fecha.GetColumnType());
            Assert.Contains("GETDATE", fecha.GetDefaultValueSql()!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void AutorizacionCredito_TieneRelacionConSocioNegocioEIndices()
        {
            var et = Modelo().FindEntityType(typeof(AutorizacionCredito))!;

            var fk = et.GetForeignKeys().Single(f => f.Properties.Single().Name == "CodigoSn");
            Assert.Equal(typeof(SocioNegocio), fk.PrincipalEntityType.ClrType);
            Assert.Equal("fk_autorizacion_credito_sn", fk.GetConstraintName());
            Assert.Null(fk.PrincipalToDependent);

            var porDocumento = et.GetIndexes().Single(i => i.GetDatabaseName() == "IX_AutorizacionCredito_Documento");
            Assert.Equal(new[] { "TipoDocumento", "EntryDocumento" }, porDocumento.Properties.Select(p => p.Name).ToArray());

            var porSocio = et.GetIndexes().Single(i => i.GetDatabaseName() == "IX_AutorizacionCredito_CodigoSN");
            Assert.Equal(new[] { "CodigoSn" }, porSocio.Properties.Select(p => p.Name).ToArray());
        }
    }
}
