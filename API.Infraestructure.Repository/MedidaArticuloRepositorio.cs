using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class MedidaArticuloRepositorio : RepositorioGenericoEfCore<MedidaArticulo, int>
    {
        public MedidaArticuloRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
