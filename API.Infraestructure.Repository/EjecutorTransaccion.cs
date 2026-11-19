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
            return EjecutarConAnidamientoAsync(_context, operacion, null);
        }

        public Task<T> EjecutarAsync<T>(Func<Task<T>> operacion, System.Data.IsolationLevel nivel)
        {
            return EjecutarConAnidamientoAsync(_context, operacion, nivel);
        }

        /// <summary>
        /// Ejecuta la operación en una transacción. Si ya hay una activa en el contexto (p. ej. la venta POS, que compone
        /// factura y cobro), la operación corre DENTRO de ella: se guardan sus cambios pero no se confirma ni se revierte
        /// aquí; eso lo hace el nivel exterior. El nivel de aislamiento pedido por una operación anidada se ignora: rige el
        /// de la transacción exterior (el aislamiento lo fija el exterior; si una operación anidada necesita, p. ej.,
        /// Serializable, el exterior debe abrirse con ese nivel).
        /// El exterior no debe atrapar excepciones de operaciones anidadas; debe dejarlas propagarse: una operación anidada
        /// fallida no revierte ni limpia el ChangeTracker, y continuar dejaría en la transacción sus cambios a medias.
        /// </summary>
        public static async Task<T> EjecutarConAnidamientoAsync<T>(DbContext context, Func<Task<T>> operacion, System.Data.IsolationLevel? nivel)
        {
            if (context.Database.CurrentTransaction is not null)
            {
                var anidado = await operacion();
                await context.SaveChangesAsync();
                return anidado;
            }

            await using var tx = nivel.HasValue
                ? await context.Database.BeginTransactionAsync(nivel.Value)
                : await context.Database.BeginTransactionAsync();
            try
            {
                var resultado = await operacion();
                await context.SaveChangesAsync();
                await tx.CommitAsync();
                return resultado;
            }
            catch
            {
                await tx.RollbackAsync();
                context.ChangeTracker.Clear(); // el contexto scoped queda con mutaciones parciales tras el rollback
                throw;
            }
        }
    }
}
