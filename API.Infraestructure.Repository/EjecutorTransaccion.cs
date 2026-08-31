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

        public async Task<T> EjecutarAsync<T>(Func<Task<T>> operacion)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();
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
