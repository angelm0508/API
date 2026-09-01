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
    }
}
