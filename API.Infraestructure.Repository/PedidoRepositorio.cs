using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class PedidoRepositorio : RepositorioGenericoEfCore<Pedido, int>
    {
        public PedidoRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
