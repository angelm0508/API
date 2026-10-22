using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    // Valida el mapeo EF de las entidades del taller sin tocar la base de datos (el modelo se construye en memoria).
    public class ModeloTallerTests
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
        public void Mecanico_MapeaTablaYClave()
        {
            var et = Modelo().FindEntityType(typeof(Mecanico))!;
            Assert.Equal("Mecanico", et.GetTableName());
            Assert.Equal(new[] { "Codigo" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
        }

        [Fact]
        public void Motocicleta_MapeaTablaPlacaUnicaYClienteComoClaveForanea()
        {
            var et = Modelo().FindEntityType(typeof(Motocicleta))!;
            Assert.Equal("Motocicleta", et.GetTableName());
            Assert.Equal(new[] { "Entry" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
            Assert.Equal("CodigoSN", et.FindProperty("CodigoSn")!.GetColumnName());
            var indicePlaca = et.GetIndexes().Single(i => i.Properties.Single().Name == "Placa");
            Assert.True(indicePlaca.IsUnique);
            Assert.Contains(et.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(SocioNegocio));
        }

        [Fact]
        public void OrdenServicio_MapeaTablaClavesForaneasYEstadoPorDefecto()
        {
            var et = Modelo().FindEntityType(typeof(OrdenServicio))!;
            Assert.Equal("OrdenServicio", et.GetTableName());
            Assert.Equal(new[] { "Entry" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
            Assert.Equal("CodigoSN", et.FindProperty("CodigoSn")!.GetColumnName());
            Assert.Equal("NombreSN", et.FindProperty("NombreSn")!.GetColumnName());

            var destinos = et.GetForeignKeys().Select(fk => fk.PrincipalEntityType.ClrType).ToHashSet();
            Assert.Contains(typeof(SocioNegocio), destinos);
            Assert.Contains(typeof(Monedum), destinos);
            Assert.Contains(typeof(NumeracionDocumentoDet), destinos);
            Assert.Contains(typeof(Motocicleta), destinos);
            Assert.Contains(typeof(Mecanico), destinos);

            Assert.Equal("R", new OrdenServicio().EstadoServicio);
        }

        [Fact]
        public void OrdenServicioDetalle_MapeaTablaYClaveCompuesta()
        {
            var et = Modelo().FindEntityType(typeof(OrdenServicioDetalle))!;
            Assert.Equal("OrdenServicioDetalle", et.GetTableName());
            Assert.Equal(new[] { "Entry", "NoLinea" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
            var destinos = et.GetForeignKeys().Select(fk => fk.PrincipalEntityType.ClrType).ToHashSet();
            Assert.Contains(typeof(Articulo), destinos);
            Assert.Contains(typeof(Almacen), destinos);
        }
    }
}
