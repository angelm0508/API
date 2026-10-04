using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class NotaCreditoDetalleRepositorio : RepositorioGenericoEfCore<NotaCreditoDetalle, (int Entry, int NoLinea)>
    {
        public NotaCreditoDetalleRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // Clave primaria compuesta real (Entry + NoLinea): FindAsync necesita ambas partes, en el
        // mismo orden en que se declaro HasKey en ApiDbTestContext.OnModelCreating.
        public override async Task<NotaCreditoDetalle?> ObtenerAsync((int Entry, int NoLinea) id)
        {
            return await DbSet.FindAsync(id.Entry, id.NoLinea);
        }
    }
}
