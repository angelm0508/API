using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class FabricanteArticuloRepositorio : RepositorioGenericoEfCore<FabricanteArticulo, int>
    {
        public FabricanteArticuloRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
