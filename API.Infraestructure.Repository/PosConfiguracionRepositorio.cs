using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class PosConfiguracionRepositorio : RepositorioGenericoEfCore<PosConfiguracion, int>
    {
        public PosConfiguracionRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
