using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class NotaCreditoRepositorio : RepositorioGenericoEfCore<NotaCredito, int>
    {
        public NotaCreditoRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
