using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class PagoEfectuadoMedioRepositorio : RepositorioGenericoEfCore<PagoEfectuadoMedio, (int Entry, int NoLinea)>
    {
        public PagoEfectuadoMedioRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        public override async Task<PagoEfectuadoMedio?> ObtenerAsync((int Entry, int NoLinea) id)
        {
            return await DbSet.FindAsync(id.Entry, id.NoLinea);
        }

        // El detalle del pago muestra el nombre y el tipo de dato de la forma de pago.
        public override Task<IQueryable<PagoEfectuadoMedio>> ObtenerTodoAsync()
        {
            return Task.FromResult(DbSet.Include(x => x.CodFormaPagoNavigation).AsQueryable());
        }
    }
}
