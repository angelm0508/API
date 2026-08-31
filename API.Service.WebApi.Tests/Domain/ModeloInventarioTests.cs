using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    // Valida el mapeo EF de las entidades de inventario sin tocar la base de datos:
    // construye el modelo en memoria y verifica tablas, claves y la FK auto-referente.
    public class ModeloInventarioTests
    {
        // Se pasan opciones explícitas con un connection string literal (nunca se abre): el ctor sin
        // parámetros usa "Name=ConnectionStrings:API_DB", que EF sólo resuelve vía IConfiguration y
        // aquí no existe. Construir el modelo (ctx.Model) NO abre conexión; si el mapeo estuviera mal
        // configurado, este acceso lanzaría una excepción.
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
        public void ExistenciaArticulo_MapeaTablaYClaveCompuesta()
        {
            var et = Modelo().FindEntityType(typeof(ExistenciaArticulo))!;
            Assert.Equal("ExistenciaArticulo", et.GetTableName());
            var pk = et.FindPrimaryKey()!;
            Assert.Equal(new[] { "CodArticulo", "CodAlmacen" }, pk.Properties.Select(p => p.Name).ToArray());
            Assert.True(et.FindProperty("RowVersion")!.IsConcurrencyToken);
        }

        [Fact]
        public void MovimientoInventario_MapeaTablaClaveYAutoReferencia()
        {
            var et = Modelo().FindEntityType(typeof(MovimientoInventario))!;
            Assert.Equal("MovimientoInventario", et.GetTableName());
            Assert.Equal(new[] { "Entry" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
            var selfFk = et.GetForeignKeys().Single(fk => fk.PrincipalEntityType == et);
            Assert.Equal("MovReversaDe", selfFk.Properties.Single().Name);
            Assert.False(selfFk.IsRequired);
        }

        [Fact]
        public void Articulo_GanaColumnasDeValuacion()
        {
            var et = Modelo().FindEntityType(typeof(Articulo))!;
            Assert.NotNull(et.FindProperty("MetodoValuacion"));
            Assert.NotNull(et.FindProperty("CostoPromedio"));
            Assert.NotNull(et.FindProperty("CostoEstandar"));
            Assert.NotNull(et.FindProperty("ValorInventario"));
        }
    }
}
