using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class UnidadMedidaArticuloRepositorio : RepositorioGenericoEfCore<UnidadMedidaArticulo, int>
    {
        public UnidadMedidaArticuloRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
