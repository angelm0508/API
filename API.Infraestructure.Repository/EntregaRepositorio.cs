using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class EntregaRepositorio : RepositorioGenericoEfCore<Entrega, int>
    {
        public EntregaRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
