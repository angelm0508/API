using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class PlantillaRepositorio : RepositorioGenericoEfCore<Plantilla, int>
    {
        public PlantillaRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
