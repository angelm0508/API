using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class PagoRecibidoMedioRepositorio : RepositorioGenericoEfCore<PagoRecibidoMedio, (int Entry, int NoLinea)>
    {
        public PagoRecibidoMedioRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        public override async Task<PagoRecibidoMedio?> ObtenerAsync((int Entry, int NoLinea) id)
        {
            return await DbSet.FindAsync(id.Entry, id.NoLinea);
        }

        // El detalle del pago muestra el nombre y el tipo de dato de la forma de pago.
        public override Task<IQueryable<PagoRecibidoMedio>> ObtenerTodoAsync()
        {
            return Task.FromResult(DbSet.Include(x => x.CodFormaPagoNavigation).AsQueryable());
        }
    }
}
