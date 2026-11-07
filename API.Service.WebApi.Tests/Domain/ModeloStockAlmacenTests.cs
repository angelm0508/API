using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    // Valida el mapeo EF del stock minimo, maximo y bloqueo por articulo y almacen sin tocar la base de datos (el modelo se construye en memoria).
    public class ModeloStockAlmacenTests
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
        public void ArticuloAlmacen_MapeaTablaClaveCompuestaYLongitudes()
        {
            var et = Modelo().FindEntityType(typeof(ArticuloAlmacen))!;
            Assert.Equal("ArticuloAlmacen", et.GetTableName());

            var clave = et.FindPrimaryKey()!;
            Assert.Equal("pk_articulo_almacen", clave.GetName());
            Assert.Equal(new[] { "CodArticulo", "CodAlmacen" }, clave.Properties.Select(p => p.Name).ToArray());

            Assert.Equal(15, et.FindProperty("CodArticulo")!.GetMaxLength());
            Assert.Equal(8, et.FindProperty("CodAlmacen")!.GetMaxLength());
            Assert.Equal(1, et.FindProperty("Bloqueado")!.GetMaxLength());
        }

        [Fact]
        public void ArticuloAlmacen_MinimoYMaximoSonDecimalNulables()
        {
            var et = Modelo().FindEntityType(typeof(ArticuloAlmacen))!;

            foreach (var nombre in new[] { "Minimo", "Maximo" })
            {
                var p = et.FindProperty(nombre)!;
                Assert.Equal(typeof(decimal?), p.ClrType);
                Assert.True(p.IsNullable);
                Assert.Equal("decimal(19,6)", p.GetColumnType().Replace(" ", ""));
            }
        }

        [Fact]
        public void ArticuloAlmacen_BloqueadoEsObligatorioConDefaultN()
        {
            var et = Modelo().FindEntityType(typeof(ArticuloAlmacen))!;

            var bloqueado = et.FindProperty("Bloqueado")!;
            Assert.Equal(typeof(string), bloqueado.ClrType);
            Assert.False(bloqueado.IsNullable);
            Assert.Equal("N", bloqueado.GetDefaultValueSql()!.Trim('(', ')', '\''));
            Assert.Equal("N", new ArticuloAlmacen().Bloqueado);
        }

        [Fact]
        public void ArticuloAlmacen_TieneRelacionConArticuloYAlmacenSinColeccionesInversas()
        {
            var et = Modelo().FindEntityType(typeof(ArticuloAlmacen))!;

            var fkArticulo = et.GetForeignKeys().Single(f => f.Properties.Single().Name == "CodArticulo");
            Assert.Equal(typeof(Articulo), fkArticulo.PrincipalEntityType.ClrType);
            Assert.Equal("fk_articulo_almacen_articulo", fkArticulo.GetConstraintName());
            Assert.Null(fkArticulo.PrincipalToDependent);
            Assert.Equal(nameof(ArticuloAlmacen.CodArticuloNavigation), fkArticulo.DependentToPrincipal!.Name);

            var fkAlmacen = et.GetForeignKeys().Single(f => f.Properties.Single().Name == "CodAlmacen");
            Assert.Equal(typeof(Almacen), fkAlmacen.PrincipalEntityType.ClrType);
            Assert.Equal("fk_articulo_almacen_almacen", fkAlmacen.GetConstraintName());
            Assert.Null(fkAlmacen.PrincipalToDependent);
            Assert.Equal(nameof(ArticuloAlmacen.CodAlmacenNavigation), fkAlmacen.DependentToPrincipal!.Name);
        }

        [Fact]
        public void ArticuloAlmacen_BorraEnCascadaAlEliminarElArticuloOElAlmacen()
        {
            // Borrar un artículo o un almacén debe llevarse su configuración de stock (coincide con el ON DELETE CASCADE del script SQL).
            var et = Modelo().FindEntityType(typeof(ArticuloAlmacen))!;

            foreach (var fk in et.GetForeignKeys())
                Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
        }
    }
}
