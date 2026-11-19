using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class BloqueoSeries : IBloqueoSeries
    {
        private readonly ApiDbTestContext _context;

        public BloqueoSeries(ApiDbTestContext context)
        {
            _context = context;
        }

        public Task BloquearAsync(int serie) => BloquearAsync(_context, serie);

        /// <summary>
        /// Toma un candado de actualización (U) sobre la fila de la serie, retenido hasta el fin de la transacción activa
        /// (HOLDLOCK). Quien lo pida después espera al commit o rollback del que lo tiene. Exige transacción activa: sin
        /// ella el candado se soltaría al terminar la sentencia.
        /// </summary>
        public static async Task BloquearAsync(DbContext context, int serie)
        {
            if (context.Database.CurrentTransaction is null)
                throw new InvalidOperationException("El candado de serie requiere una transacción activa.");

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM dbo.NumeracionDocumentoDet WITH (UPDLOCK, HOLDLOCK) WHERE Serie = {serie}");
        }
    }
}
