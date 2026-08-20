using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class UsuarioRepositorio : RepositorioGenericoEfCore<Usuario, int>
    {
        public UsuarioRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
