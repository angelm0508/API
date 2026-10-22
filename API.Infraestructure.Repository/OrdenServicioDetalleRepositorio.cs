using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class OrdenServicioDetalleRepositorio : RepositorioGenericoEfCore<OrdenServicioDetalle, (int Entry, int NoLinea)>
    {
        public OrdenServicioDetalleRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // Clave primaria compuesta real (Entry + NoLinea): FindAsync necesita ambas partes, en el
        // mismo orden en que se declaró HasKey en ApiDbTestContext.OnModelCreating.
        public override async Task<OrdenServicioDetalle?> ObtenerAsync((int Entry, int NoLinea) id)
        {
            return await DbSet.FindAsync(id.Entry, id.NoLinea);
        }
    }
}
