using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class MonedaRepositorio : RepositorioGenericoEfCore<Monedum, string>
    {
        public MonedaRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
