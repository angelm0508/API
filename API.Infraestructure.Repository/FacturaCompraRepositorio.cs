using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class FacturaCompraRepositorio : RepositorioGenericoEfCore<FacturaCompra, int>
    {
        public FacturaCompraRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
