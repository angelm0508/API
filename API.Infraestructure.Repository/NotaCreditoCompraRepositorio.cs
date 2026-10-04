using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class NotaCreditoCompraRepositorio : RepositorioGenericoEfCore<NotaCreditoCompra, int>
    {
        public NotaCreditoCompraRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
