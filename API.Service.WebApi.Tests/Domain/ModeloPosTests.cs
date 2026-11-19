using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    // Valida el contrato de la entidad PosConfiguracion y de los campos POS del articulo, y su mapeo EF, sin tocar la base de datos.
    public class ModeloPosTests
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
        public void Articulo_TieneCodigoBarrasNulableYMostrarEnPosConDefectoN()
        {
            Assert.Equal(typeof(string), typeof(Articulo).GetProperty(nameof(Articulo.CodigoBarras))?.PropertyType);
            Assert.Null(new Articulo().CodigoBarras);
            Assert.Equal(typeof(string), typeof(Articulo).GetProperty(nameof(Articulo.MostrarEnPos))?.PropertyType);
            Assert.Equal("N", new Articulo().MostrarEnPos);
        }

        [Fact]
        public void PosConfiguracion_TieneLasPropiedadesDelContrato()
        {
            var tipos = new Dictionary<string, Type>
            {
                ["Id"] = typeof(int),
                ["SeriePago"] = typeof(int?),
                ["SerieFactura"] = typeof(int?),
                ["CodAlmacen"] = typeof(string),
                ["CodigoImpuesto"] = typeof(string),
                ["ClienteDefecto"] = typeof(string),
                ["FormaPagoEfectivo"] = typeof(string),
                ["MonedaDoc"] = typeof(string),
            };

            foreach (var (nombre, tipo) in tipos)
                Assert.Equal(tipo, typeof(PosConfiguracion).GetProperty(nombre)?.PropertyType);

            var cfg = new PosConfiguracion();
            Assert.Null(cfg.SeriePago);
            Assert.Null(cfg.SerieFactura);
            Assert.Null(cfg.CodAlmacen);
            Assert.Null(cfg.CodigoImpuesto);
            Assert.Null(cfg.ClienteDefecto);
            Assert.Null(cfg.FormaPagoEfectivo);
            Assert.Null(cfg.MonedaDoc);
        }

        [Fact]
        public void ModeloEf_MapeaCodigoBarras50YMostrarEnPos1ConDefectoN()
        {
            var et = Modelo().FindEntityType(typeof(Articulo))!;

            var barras = et.FindProperty(nameof(Articulo.CodigoBarras))!;
            Assert.True(barras.IsNullable);
            Assert.Equal(50, barras.GetMaxLength());

            var mostrar = et.FindProperty(nameof(Articulo.MostrarEnPos))!;
            Assert.False(mostrar.IsNullable);
            Assert.Equal(1, mostrar.GetMaxLength());
            Assert.Equal("N", mostrar.GetDefaultValueSql()!.Trim('(', ')', '\''));
        }

        [Fact]
        public void ModeloEf_MapeaPosConfiguracionConLongitudesDeLasTablasReferenciadas()
        {
            var et = Modelo().FindEntityType(typeof(PosConfiguracion))!;

            Assert.Equal("PosConfiguracion", et.GetTableName());
            Assert.Equal(nameof(PosConfiguracion.Id), Assert.Single(et.FindPrimaryKey()!.Properties).Name);
            Assert.Equal(ValueGenerated.Never, et.FindProperty(nameof(PosConfiguracion.Id))!.ValueGenerated);

            // Mismas longitudes que Almacen.Codigo, Impuesto.Codigo, SocioNegocio.Codigo, FormaPago.Codigo y Moneda.Codigo
            Assert.Equal(8, et.FindProperty(nameof(PosConfiguracion.CodAlmacen))!.GetMaxLength());
            Assert.Equal(8, et.FindProperty(nameof(PosConfiguracion.CodigoImpuesto))!.GetMaxLength());
            Assert.Equal(15, et.FindProperty(nameof(PosConfiguracion.ClienteDefecto))!.GetMaxLength());
            Assert.Equal(10, et.FindProperty(nameof(PosConfiguracion.FormaPagoEfectivo))!.GetMaxLength());
            Assert.Equal(3, et.FindProperty(nameof(PosConfiguracion.MonedaDoc))!.GetMaxLength());

            foreach (var nombre in new[] { "SeriePago", "SerieFactura", "CodAlmacen", "CodigoImpuesto", "ClienteDefecto", "FormaPagoEfectivo", "MonedaDoc" })
                Assert.True(et.FindProperty(nombre)!.IsNullable, nombre);
        }
    }
}
