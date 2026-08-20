using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class GrupoMedidaArticuloRepositorio : RepositorioGenericoEfCore<GrupoMedidaArticulo, int>
    {
        public GrupoMedidaArticuloRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
