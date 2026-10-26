using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class CondicionPagoRepositorio : RepositorioGenericoEfCore<CondicionPago, string>
    {
        public CondicionPagoRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
