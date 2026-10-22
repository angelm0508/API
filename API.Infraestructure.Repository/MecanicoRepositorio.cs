using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class MecanicoRepositorio : RepositorioGenericoEfCore<Mecanico, string>
    {
        public MecanicoRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
