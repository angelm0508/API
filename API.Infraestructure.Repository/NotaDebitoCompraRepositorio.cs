using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class NotaDebitoCompraRepositorio : RepositorioGenericoEfCore<NotaDebitoCompra, int>
    {
        public NotaDebitoCompraRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
