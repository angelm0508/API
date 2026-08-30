using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class EntregaCompraRepositorio : RepositorioGenericoEfCore<EntregaCompra, int>
    {
        public EntregaCompraRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
