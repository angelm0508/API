using API.Domain.Entity.Models;
using API.Domain.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class EjecutorTransaccion : IEjecutorTransaccion
    {
        private readonly ApiDbTestContext _context;

        public EjecutorTransaccion(ApiDbTestContext context)
        {
            _context = context;
        }

        public Task<T> EjecutarAsync<T>(Func<Task<T>> operacion)
        {
            // Nivel por defecto de la conexión (READ COMMITTED): comportamiento histórico.
            return EjecutarInternoAsync(operacion, null);
        }

        public Task<T> EjecutarAsync<T>(Func<Task<T>> operacion, System.Data.IsolationLevel nivel)
        {
            return EjecutarInternoAsync(operacion, nivel);
        }

        private async Task<T> EjecutarInternoAsync<T>(Func<Task<T>> operacion, System.Data.IsolationLevel? nivel)
        {
            await using var tx = nivel.HasValue
                ? await _context.Database.BeginTransactionAsync(nivel.Value)
                : await _context.Database.BeginTransactionAsync();
            try
            {
                var resultado = await operacion();
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return resultado;
            }
            catch
            {
                await tx.RollbackAsync();
                _context.ChangeTracker.Clear(); // el contexto scoped queda con mutaciones parciales tras el rollback
                throw;
            }
        }
    }
}
