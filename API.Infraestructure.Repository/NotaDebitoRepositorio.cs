using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class NotaDebitoRepositorio : RepositorioGenericoEfCore<NotaDebito, int>
    {
        public NotaDebitoRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
