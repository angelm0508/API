using API.Domain.Entity.Models;
using API.Infraestructure.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace API.Service.WebApi.Tests.Infraestructure
{
    // Sin base de datos: opciones con un connection string que nunca se abre (mismo patrón que
    // ActualizarAsyncPropiedadesProtegidasTests). Attach / Add / Entry son operaciones en memoria.
    public class DescartarCambiosPendientesTests
    {
        private static readonly DbContextOptions<ApiDbTestContext> _opciones =
            new DbContextOptionsBuilder<ApiDbTestContext>()
                .UseSqlServer("Server=localhost;Database=API_DB_TEST;Trusted_Connection=True;TrustServerCertificate=True")
                .Options;

        [Fact]
        public void DescartaLasAgregadasYModificadas_YDejaIntactoLoDemas()
        {
            using var ctx = new ApiDbTestContext(_opciones);
            var repo = new ExistenciaArticuloRepositorio(ctx);

            var modificada = new ExistenciaArticulo { CodArticulo = "A1", CodAlmacen = "01", RowVersion = new byte[8] };
            ctx.Attach(modificada);
            modificada.Comprometido = 10m;                                   // Modified
            var sinCambio = new ExistenciaArticulo { CodArticulo = "A2", CodAlmacen = "01", RowVersion = new byte[8] };
            ctx.Attach(sinCambio);                                            // Unchanged
            var nueva = new ExistenciaArticulo { CodArticulo = "A3", CodAlmacen = "01", RowVersion = new byte[8], Comprometido = 5m };
            ctx.Add(nueva);                                                   // Added
            var articulo = new Articulo { Codigo = "X", Nombre = "Original" };
            ctx.Attach(articulo);
            articulo.Nombre = "Cambiado";                                     // Modified de OTRO tipo

            repo.DescartarCambiosPendientes();

            Assert.Equal(EntityState.Detached, ctx.Entry(modificada).State);
            Assert.Equal(EntityState.Detached, ctx.Entry(nueva).State);
            Assert.Equal(EntityState.Unchanged, ctx.Entry(sinCambio).State);
            Assert.Equal(EntityState.Modified, ctx.Entry(articulo).State);
            Assert.Single(ctx.ChangeTracker.Entries<ExistenciaArticulo>());
        }

        [Fact]
        public void SinPendientes_NoHaceNada()
        {
            using var ctx = new ApiDbTestContext(_opciones);
            var repo = new ExistenciaArticuloRepositorio(ctx);

            repo.DescartarCambiosPendientes();

            Assert.Empty(ctx.ChangeTracker.Entries());
        }
    }
}
