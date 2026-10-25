using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class PagoEfectuadoRepositorio : RepositorioGenericoEfCore<PagoEfectuado, int>
    {
        public PagoEfectuadoRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
