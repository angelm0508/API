using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class PaisRepositorio : RepositorioGenericoEfCore<Pai, string>
    {
        public PaisRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
