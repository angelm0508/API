using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class ImpuestoRepositorio : RepositorioGenericoEfCore<Impuesto, string>
    {
        public ImpuestoRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
