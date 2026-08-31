using API.Domain.Entity.Models;
using API.Infraestructure.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Xunit;

namespace API.Service.WebApi.Tests.Infraestructure
{
    // Regresión del hallazgo CRÍTICO: las 4 columnas de valuación de Articulo
    // (MetodoValuacion / CostoPromedio / CostoEstandar / ValorInventario) NO están en
    // ArticuloActualizarDTO, así que el objeto mapeado desde ese DTO llega con esos campos en su
    // valor CLR por defecto (null / 0). Antes del blindaje, el bucle de copia de ActualizarAsync
    // los escribía sobre la entidad rastreada -> UPDATE ... SET MetodoValuacion = NULL sobre
    // columnas NOT NULL -> el PUT de todo artículo fallaba. ArticuloRepositorio ahora las declara
    // en PropiedadesNoActualizables y CopiarPropiedadesActualizables debe saltarlas.
    //
    // Sin base de datos: se usan opciones con un connection string literal que nunca se abre (mismo
    // patrón que ModeloInventarioTests). Attach / Entry / Property().CurrentValue son en memoria.
    public class ActualizarAsyncPropiedadesProtegidasTests
    {
        private static readonly DbContextOptions<ApiDbTestContext> _opciones =
            new DbContextOptionsBuilder<ApiDbTestContext>()
                .UseSqlServer("Server=localhost;Database=API_DB_TEST;Trusted_Connection=True;TrustServerCertificate=True")
                .Options;

        // Subclase de prueba: expone el método protegido sin reflexión.
        private sealed class ArticuloRepositorioProbe : ArticuloRepositorio
        {
            public ArticuloRepositorioProbe(ApiDbTestContext ctx) : base(ctx) { }

            public void CopiarPublico(EntityEntry entrada, Articulo origen) =>
                CopiarPropiedadesActualizables(entrada, origen);
        }

        [Fact]
        public void CopiarPropiedadesActualizables_NoPisaColumnasDeValuacion_PeroSiCopiaNombre()
        {
            using var ctx = new ApiDbTestContext(_opciones);

            var rastreado = new Articulo
            {
                Codigo = "ART1",
                Nombre = "Original",
                MetodoValuacion = "E",
                CostoPromedio = 12m,
                CostoEstandar = 20m,
                ValorInventario = 999m,
            };
            ctx.Attach(rastreado);

            var repo = new ArticuloRepositorioProbe(ctx);

            // Objeto con la forma de lo que produce _mapper.Map<Articulo>(ArticuloActualizarDTO):
            // sólo trae los campos del CRUD; los de valuación quedan en su valor CLR por defecto
            // (MetodoValuacion = null, costos = 0).
            var entrante = new Articulo
            {
                Codigo = "ART1",
                Nombre = "Nuevo",
            };

            repo.CopiarPublico(ctx.Entry(rastreado), entrante);

            Assert.Equal("E", rastreado.MetodoValuacion);
            Assert.Equal(12m, rastreado.CostoPromedio);
            Assert.Equal(20m, rastreado.CostoEstandar);
            Assert.Equal(999m, rastreado.ValorInventario);
            Assert.Equal("Nuevo", rastreado.Nombre);
        }
    }
}
