using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class PagoRecibidoFacturaRepositorio : RepositorioGenericoEfCore<PagoRecibidoFactura, (int Entry, int NoLinea)>
    {
        public PagoRecibidoFacturaRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // Clave primaria compuesta real (Entry + NoLinea), en el orden de HasKey en ApiDbTestContext.
        public override async Task<PagoRecibidoFactura?> ObtenerAsync((int Entry, int NoLinea) id)
        {
            return await DbSet.FindAsync(id.Entry, id.NoLinea);
        }

        // El detalle del pago muestra el número y el tipo del documento cobrado.
        public override Task<IQueryable<PagoRecibidoFactura>> ObtenerTodoAsync()
        {
            return Task.FromResult(DbSet.Include(x => x.FacturaNavigation).AsQueryable());
        }
    }
}
