using System.Data;
using API.Infraestructure.Repository;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace API.Service.WebApi.Tests.Infraestructure
{
    // Transacciones anidables de EjecutorTransaccion (POS: la venta compone factura y cobro en una sola transacción).
    // Se prueba contra SQLite en memoria con un contexto mínimo: EjecutarConAnidamientoAsync recibe un DbContext.
    public class EjecutorTransaccionAnidadoTests : IDisposable
    {
        private class Fila
        {
            public int Id { get; set; }
            public string Texto { get; set; } = "";
        }

        private class PruebaContext : DbContext
        {
            public PruebaContext(DbContextOptions<PruebaContext> options) : base(options) { }
            public DbSet<Fila> Filas => Set<Fila>();
        }

        private readonly SqliteConnection _conexion;

        public EjecutorTransaccionAnidadoTests()
        {
            _conexion = new SqliteConnection("DataSource=:memory:");
            _conexion.Open();
            using var ctx = NuevoContexto();
            ctx.Database.EnsureCreated();
        }

        public void Dispose() => _conexion.Dispose();

        private PruebaContext NuevoContexto() =>
            new(new DbContextOptionsBuilder<PruebaContext>().UseSqlite(_conexion).Options);

        private static Task<int> Agregar(PruebaContext ctx, string texto, int devuelve = 0)
        {
            ctx.Filas.Add(new Fila { Texto = texto });
            return Task.FromResult(devuelve);
        }

        private List<string> Textos()
        {
            using var ctx = NuevoContexto();
            return ctx.Filas.AsNoTracking().OrderBy(f => f.Texto).Select(f => f.Texto).ToList();
        }

        [Fact]
        public async Task SinTransaccionActiva_Confirma()
        {
            using var ctx = NuevoContexto();

            var r = await EjecutorTransaccion.EjecutarConAnidamientoAsync(ctx, () => Agregar(ctx, "A", 7), null);

            Assert.Equal(7, r);
            Assert.Null(ctx.Database.CurrentTransaction);
            Assert.Equal(new[] { "A" }, Textos());
        }

        [Fact]
        public async Task SinTransaccionActiva_Revierte_Y_LimpiaElChangeTracker()
        {
            using var ctx = NuevoContexto();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                EjecutorTransaccion.EjecutarConAnidamientoAsync<int>(ctx, async () =>
                {
                    await Agregar(ctx, "A");
                    throw new InvalidOperationException("x");
                }, null));

            Assert.Equal("x", ex.Message);
            Assert.Empty(Textos());
            Assert.Empty(ctx.ChangeTracker.Entries());
            Assert.Null(ctx.Database.CurrentTransaction);
        }

        [Fact]
        public async Task Anidada_ComparteLaTransaccionDelExterior_SinConfirmarNiCerrar()
        {
            using var ctx = NuevoContexto();
            string? idAntes = null, idDespues = null;
            var anidadaCorrio = false;
            var fila = new Fila { Texto = "B" };

            await EjecutorTransaccion.EjecutarConAnidamientoAsync(ctx, async () =>
            {
                idAntes = ctx.Database.CurrentTransaction!.TransactionId.ToString();
                await EjecutorTransaccion.EjecutarConAnidamientoAsync(ctx, async () =>
                {
                    anidadaCorrio = true;
                    ctx.Filas.Add(fila);
                    return await Task.FromResult(1);
                }, null);
                // La interior guardó sus cambios (su SaveChanges ya asignó la clave) pero no cerró la transacción: sigue la misma.
                Assert.NotEqual(0, fila.Id);
                Assert.NotNull(ctx.Database.CurrentTransaction);
                idDespues = ctx.Database.CurrentTransaction!.TransactionId.ToString();
                return 0;
            }, null);

            Assert.True(anidadaCorrio);
            Assert.NotNull(idAntes);
            Assert.Equal(idAntes, idDespues);
            Assert.Null(ctx.Database.CurrentTransaction); // el exterior sí cerró
            Assert.Equal(new[] { "B" }, Textos());
        }

        [Fact]
        public async Task RollbackDelExterior_DeshaceLoAnidado()
        {
            using var ctx = NuevoContexto();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                EjecutorTransaccion.EjecutarConAnidamientoAsync<int>(ctx, async () =>
                {
                    await Agregar(ctx, "A");
                    await EjecutorTransaccion.EjecutarConAnidamientoAsync(ctx, () => Agregar(ctx, "B"), null);
                    throw new InvalidOperationException("falla exterior");
                }, null));

            Assert.Empty(Textos());
            Assert.Null(ctx.Database.CurrentTransaction);
        }

        [Fact]
        public async Task FalloDeLaAnidada_DeshaceTodo_Y_LlegaAlLlamadorDelExterior()
        {
            using var ctx = NuevoContexto();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                EjecutorTransaccion.EjecutarConAnidamientoAsync<int>(ctx, async () =>
                {
                    await Agregar(ctx, "A");
                    return await EjecutorTransaccion.EjecutarConAnidamientoAsync<int>(ctx, async () =>
                    {
                        await Agregar(ctx, "B");
                        throw new InvalidOperationException("falla interior");
                    }, null);
                }, null));

            Assert.Equal("falla interior", ex.Message);
            Assert.Empty(Textos());
            Assert.Empty(ctx.ChangeTracker.Entries());
            Assert.Null(ctx.Database.CurrentTransaction);
        }

        [Fact]
        public async Task NivelDeAislamientoDeLaAnidada_SeIgnora()
        {
            using var ctx = NuevoContexto();

            var r = await EjecutorTransaccion.EjecutarConAnidamientoAsync(ctx, async () =>
            {
                return await EjecutorTransaccion.EjecutarConAnidamientoAsync(
                    ctx, () => Agregar(ctx, "B", 5), IsolationLevel.Serializable);
            }, null);

            Assert.Equal(5, r);
            Assert.Equal(new[] { "B" }, Textos());
        }
    }
}
