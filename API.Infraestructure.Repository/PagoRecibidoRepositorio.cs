using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class PagoRecibidoRepositorio : RepositorioGenericoEfCore<PagoRecibido, int>
    {
        public PagoRecibidoRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
