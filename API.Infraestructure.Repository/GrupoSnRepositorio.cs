using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class GrupoSnRepositorio : RepositorioGenericoEfCore<GrupoSn, int>
    {
        public GrupoSnRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        public override async Task<GrupoSn?> ObtenerAsync(int id)
        {
            return await DbSet.FindAsync((short)id);
        }
    }
}
