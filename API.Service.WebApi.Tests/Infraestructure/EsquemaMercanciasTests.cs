using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace API.Service.WebApi.Tests.Infraestructure
{
    // Valida que el modelo EF conoce las 4 entidades de Entrada/Salida de Mercancias (INV-4)
    // con su clave primaria, sin tocar la base de datos: se pasan opciones con un connection
    // string literal que nunca se abre (construir ctx.Model no abre conexión).
    public class EsquemaMercanciasTests
    {
        private static ApiDbTestContext Contexto()
        {
            var options = new DbContextOptionsBuilder<ApiDbTestContext>()
                .UseSqlServer("Server=(localdb)\\nunca;Database=x;Trust Server Certificate=True")
                .Options;
            return new ApiDbTestContext(options);
        }

        [Theory]
        [InlineData(typeof(EntradaMercancia))]
        [InlineData(typeof(EntradaMercanciaDetalle))]
        [InlineData(typeof(SalidaMercancia))]
        [InlineData(typeof(SalidaMercanciaDetalle))]
        public void ModeloConoceLaEntidadConPk(System.Type tipo)
        {
            using var ctx = Contexto();
            var et = ctx.Model.FindEntityType(tipo);
            Assert.NotNull(et);
            Assert.NotNull(et!.FindPrimaryKey());
        }

        // Protege C-1 / I-2: el MaxLength de CodArticulo/CodAlmacen de cada detalle debe
        // coincidir con el de la PK de Articulo/Almacen (si divergen, el .sql no aplica).
        [Theory]
        [InlineData(typeof(EntradaMercanciaDetalle))]
        [InlineData(typeof(SalidaMercanciaDetalle))]
        public void DetalleAlineaMaxLengthConLaTablaPadre(System.Type tipo)
        {
            using var ctx = Contexto();
            var det = ctx.Model.FindEntityType(tipo)!;
            var art = ctx.Model.FindEntityType(typeof(Articulo))!;
            var alm = ctx.Model.FindEntityType(typeof(Almacen))!;
            Assert.Equal(art.FindProperty("Codigo")!.GetMaxLength(), det.FindProperty("CodArticulo")!.GetMaxLength());
            Assert.Equal(alm.FindProperty("Codigo")!.GetMaxLength(), det.FindProperty("CodAlmacen")!.GetMaxLength());
        }
    }
}
