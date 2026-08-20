using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class GrupoArticuloRepositorio : RepositorioGenericoEfCore<GrupoArticulo, int>
    {
        public GrupoArticuloRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        public override async Task<GrupoArticulo?> ObtenerAsync(int id)
        {
            return await DbSet.FindAsync((short)id);
        }
    }
}
