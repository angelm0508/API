using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class SalidaMercanciaRepositorio : RepositorioGenericoEfCore<SalidaMercancia, int>
    {
        public SalidaMercanciaRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
