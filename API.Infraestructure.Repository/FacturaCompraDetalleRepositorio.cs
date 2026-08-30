using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class FacturaCompraDetalleRepositorio : RepositorioGenericoEfCore<FacturaCompraDetalle, (int Entry, int NoLinea)>
    {
        public FacturaCompraDetalleRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // Clave primaria compuesta real (Entry + NoLinea): FindAsync necesita ambas partes, en el
        // mismo orden en que se declaró HasKey en ApiDbTestContext.OnModelCreating.
        public override async Task<FacturaCompraDetalle?> ObtenerAsync((int Entry, int NoLinea) id)
        {
            return await DbSet.FindAsync(id.Entry, id.NoLinea);
        }
    }
}
