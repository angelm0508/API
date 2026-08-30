using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class CotizacionRepositorio : RepositorioGenericoEfCore<Cotizacion, int>
    {
        public CotizacionRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
