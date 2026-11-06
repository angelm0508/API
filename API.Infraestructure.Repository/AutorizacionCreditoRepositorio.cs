using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class AutorizacionCreditoRepositorio : RepositorioGenericoEfCore<AutorizacionCredito, int>
    {
        public AutorizacionCreditoRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
