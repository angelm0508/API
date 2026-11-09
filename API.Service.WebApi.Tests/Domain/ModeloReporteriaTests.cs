using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    // Valida el mapeo EF de las vistas guardadas de reportes sin tocar la base de datos (el modelo se construye en memoria).
    public class ModeloReporteriaTests
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
        public void ReporteVista_MapeaTablaClaveYLongitudes()
        {
            var et = Modelo().FindEntityType(typeof(ReporteVista))!;
            Assert.Equal("ReporteVista", et.GetTableName());

            var clave = et.FindPrimaryKey()!;
            Assert.Equal("pk_reporte_vista", clave.GetName());
            Assert.Equal(new[] { "Entry" }, clave.Properties.Select(p => p.Name).ToArray());

            Assert.Equal(50, et.FindProperty("CodigoReporte")!.GetMaxLength());
            Assert.Equal(100, et.FindProperty("Nombre")!.GetMaxLength());
            Assert.False(et.FindProperty("Configuracion")!.IsNullable);
        }

        [Fact]
        public void ReporteVista_TieneIndiceUnicoPorUsuarioReporteYNombre()
        {
            var et = Modelo().FindEntityType(typeof(ReporteVista))!;
            var indice = et.GetIndexes().Single(i => i.IsUnique);

            Assert.Equal(new[] { "UsuarioId", "CodigoReporte", "Nombre" }, indice.Properties.Select(p => p.Name).ToArray());
            Assert.Equal("uq_reporte_vista", indice.GetDatabaseName());
        }

        [Fact]
        public void ReporteVista_LasFechasTienenDefaultGetdate()
        {
            var et = Modelo().FindEntityType(typeof(ReporteVista))!;

            foreach (var nombre in new[] { "FechaCreacion", "FechaActualizacion" })
            {
                var p = et.FindProperty(nombre)!;
                Assert.Equal(typeof(DateTime), p.ClrType);
                Assert.Contains("getdate", p.GetDefaultValueSql()!, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void ReporteVista_LlaveForaneaAUsuarioConCascada()
        {
            var et = Modelo().FindEntityType(typeof(ReporteVista))!;
            var fk = et.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(Usuario));

            Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
            Assert.Equal("fk_reporte_vista_usuario", fk.GetConstraintName());
            Assert.Equal("UsuarioId", fk.Properties.Single().Name);
        }
    }
}
