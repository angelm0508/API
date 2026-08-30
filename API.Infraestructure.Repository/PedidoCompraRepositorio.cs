using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class PedidoCompraRepositorio : RepositorioGenericoEfCore<PedidoCompra, int>
    {
        public PedidoCompraRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
