using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class PagoEfectuadoFacturaRepositorio : RepositorioGenericoEfCore<PagoEfectuadoFactura, (int Entry, int NoLinea)>
    {
        public PagoEfectuadoFacturaRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // Clave primaria compuesta real (Entry + NoLinea), en el orden de HasKey en ApiDbTestContext.
        public override async Task<PagoEfectuadoFactura?> ObtenerAsync((int Entry, int NoLinea) id)
        {
            return await DbSet.FindAsync(id.Entry, id.NoLinea);
        }

        // El detalle del pago muestra el número y el tipo del documento pagado.
        public override Task<IQueryable<PagoEfectuadoFactura>> ObtenerTodoAsync()
        {
            return Task.FromResult(DbSet.Include(x => x.FacturaCompraNavigation).AsQueryable());
        }
    }
}
