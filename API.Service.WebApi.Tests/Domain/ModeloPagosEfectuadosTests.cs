using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    // Valida el mapeo EF de las entidades de pagos efectuados sin tocar la base de datos (el modelo se construye en memoria).
    public class ModeloPagosEfectuadosTests
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
        public void PagoEfectuado_MapeaTablaClaveYColumnasDeSocio()
        {
            var et = Modelo().FindEntityType(typeof(PagoEfectuado))!;
            Assert.Equal("PagoEfectuado", et.GetTableName());
            Assert.Equal(new[] { "Entry" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
            Assert.Equal("CodigoSN", et.FindProperty("CodigoSn")!.GetColumnName());
            Assert.Equal("NombreSN", et.FindProperty("NombreSn")!.GetColumnName());
            Assert.Equal("decimal(19,6)", et.FindProperty("TotalPago")!.GetColumnType());
            Assert.Equal("46", et.FindProperty("TipoObjeto")!.GetDefaultValueSql()!.Trim('(', ')', '\''));
        }

        [Fact]
        public void PagoEfectuado_ColeccionesFacturasYMediosUsanEntryComoClaveForanea()
        {
            var et = Modelo().FindEntityType(typeof(PagoEfectuado))!;
            var facturas = et.FindNavigation("Facturas")!;
            Assert.Equal(typeof(PagoEfectuadoFactura), facturas.TargetEntityType.ClrType);
            Assert.Equal(new[] { "Entry" }, facturas.ForeignKey.Properties.Select(p => p.Name).ToArray());
            var medios = et.FindNavigation("Medios")!;
            Assert.Equal(typeof(PagoEfectuadoMedio), medios.TargetEntityType.ClrType);
            Assert.Equal(new[] { "Entry" }, medios.ForeignKey.Properties.Select(p => p.Name).ToArray());
        }

        [Fact]
        public void PagoEfectuadoFactura_ClaveCompuestaYRelacionConFacturaCompra()
        {
            var et = Modelo().FindEntityType(typeof(PagoEfectuadoFactura))!;
            Assert.Equal("PagoEfectuadoFactura", et.GetTableName());
            Assert.Equal(new[] { "Entry", "NoLinea" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
            var fk = et.GetForeignKeys().Single(f => f.Properties.Single().Name == "FacturaCompraEntry");
            Assert.Equal(typeof(FacturaCompra), fk.PrincipalEntityType.ClrType);
            Assert.Equal("decimal(19,6)", et.FindProperty("MontoAplicado")!.GetColumnType());
        }

        [Fact]
        public void PagoEfectuadoMedio_ClaveCompuestaYRelacionConFormaPago()
        {
            var et = Modelo().FindEntityType(typeof(PagoEfectuadoMedio))!;
            Assert.Equal("PagoEfectuadoMedio", et.GetTableName());
            Assert.Equal(new[] { "Entry", "NoLinea" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
            var fk = et.GetForeignKeys().Single(f => f.Properties.Single().Name == "CodFormaPago");
            Assert.Equal(typeof(FormaPago), fk.PrincipalEntityType.ClrType);
            Assert.Equal("decimal(19,6)", et.FindProperty("Monto")!.GetColumnType());
        }
    }
}
