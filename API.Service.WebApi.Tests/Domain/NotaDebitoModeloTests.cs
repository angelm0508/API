using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    /// <summary>
    /// Verifica el mapeo EF de las notas de debito sin base de datos: claves, tablas y
    /// longitudes tienen que coincidir con sql/2026-10-04-notas-debito.sql.
    /// </summary>
    public class NotaDebitoModeloTests
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
        [InlineData(typeof(NotaDebito), "NotaDebito", "Entry", "7")]
        [InlineData(typeof(NotaDebitoCompra), "NotaDebitoCompra", "Entry", "14")]
        public void Encabezado_MapeaTablaClaveYDefaultDeTipoObjeto(Type tipo, string tabla, string clave, string tipoObjeto)
        {
            var entidad = Modelo().FindEntityType(tipo)!;

            Assert.Equal(tabla, entidad.GetTableName());
            Assert.Equal(new[] { clave }, entidad.FindPrimaryKey()!.Properties.Select(p => p.Name));
            Assert.Equal($"('{tipoObjeto}')", entidad.FindProperty("TipoObjeto")!.GetDefaultValueSql());
            Assert.Equal("CodigoSN", entidad.FindProperty("CodigoSn")!.GetColumnName());
            Assert.Equal("NombreSN", entidad.FindProperty("NombreSn")!.GetColumnName());
            Assert.Equal(200, entidad.FindProperty("NombreSn")!.GetMaxLength());
            Assert.DoesNotContain(entidad.GetProperties(), p => p.Name == "EstadoInv");
        }

        [Theory]
        [InlineData(typeof(NotaDebitoDetalle), "NotaDebitoDetalle", "7")]
        [InlineData(typeof(NotaDebitoCompraDetalle), "NotaDebitoCompraDetalle", "14")]
        public void Detalle_MapeaClaveCompuestaSinArticuloNiAlmacen(Type tipo, string tabla, string tipoObjeto)
        {
            var entidad = Modelo().FindEntityType(tipo)!;

            Assert.Equal(tabla, entidad.GetTableName());
            Assert.Equal(new[] { "Entry", "NoLinea" }, entidad.FindPrimaryKey()!.Properties.Select(p => p.Name));
            Assert.Equal(200, entidad.FindProperty("Descripcion")!.GetMaxLength());
            Assert.False(entidad.FindProperty("Descripcion")!.IsNullable);
            Assert.False(entidad.FindProperty("CodigoImpuesto")!.IsNullable);
            Assert.Equal($"('{tipoObjeto}')", entidad.FindProperty("TipoObjeto")!.GetDefaultValueSql());
            Assert.DoesNotContain(entidad.GetProperties(), p => p.Name is "CodArticulo" or "CodAlmacen");
        }
    }
}
