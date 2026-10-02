using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class ModuloRepositorio : RepositorioGenericoEfCore<Modulo, string>
    {
        public ModuloRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
