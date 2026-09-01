using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class EntradaMercanciaRepositorio : RepositorioGenericoEfCore<EntradaMercancia, int>
    {
        public EntradaMercanciaRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
