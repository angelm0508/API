using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class FacturaRepositorio : RepositorioGenericoEfCore<Factura, int>
    {
        public FacturaRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
