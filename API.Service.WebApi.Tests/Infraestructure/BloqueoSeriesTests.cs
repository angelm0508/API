using API.Infraestructure.Repository;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace API.Service.WebApi.Tests.Infraestructure
{
    // Candado de series del POS. La sentencia (UPDLOCK, HOLDLOCK) es propia de SQL Server y se verifica con SQL real;
    // aquí solo se prueba la guarda: fuera de una transacción el candado se soltaría al terminar la sentencia y no serviría.
    public class BloqueoSeriesTests : IDisposable
    {
        private class PruebaContext : DbContext
        {
            public PruebaContext(DbContextOptions<PruebaContext> options) : base(options) { }
        }

        private readonly SqliteConnection _conexion;

        public BloqueoSeriesTests()
        {
            _conexion = new SqliteConnection("DataSource=:memory:");
            _conexion.Open();
        }

        public void Dispose() => _conexion.Dispose();

        private PruebaContext NuevoContexto() =>
            new(new DbContextOptionsBuilder<PruebaContext>().UseSqlite(_conexion).Options);

        [Fact]
        public async Task SinTransaccionActiva_Lanza()
        {
            using var ctx = NuevoContexto();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => BloqueoSeries.BloquearAsync(ctx, 10));

            Assert.Equal("El candado de serie requiere una transacción activa.", ex.Message);
        }
    }
}
