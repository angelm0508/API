using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class UsuarioPermisoRepositorio : RepositorioGenericoEfCore<UsuarioPermiso, (int UsuarioId, string CodigoModulo)>
    {
        public UsuarioPermisoRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        public override async Task<UsuarioPermiso?> ObtenerAsync((int UsuarioId, string CodigoModulo) id)
        {
            return await DbSet.FindAsync(id.UsuarioId, id.CodigoModulo);
        }
    }
}
