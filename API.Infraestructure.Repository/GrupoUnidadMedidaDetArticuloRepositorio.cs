using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class GrupoUnidadMedidaDetArticuloRepositorio : RepositorioGenericoEfCore<GrupoUnidadMedidaDetArticulo, (int GrpMedidaEntry, int NumLinea)>
    {
        public GrupoUnidadMedidaDetArticuloRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // Clave primaria compuesta real (GrpMedidaEntry + NumLinea): FindAsync necesita ambas partes,
        // en el mismo orden en que se declaró HasKey en ApiDbTestContext.OnModelCreating.
        public override async Task<GrupoUnidadMedidaDetArticulo?> ObtenerAsync((int GrpMedidaEntry, int NumLinea) id)
        {
            return await DbSet.FindAsync(id.GrpMedidaEntry, id.NumLinea);
        }
    }
}
