using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class GrupoUnidadMedidaArticuloRepositorio : RepositorioGenericoEfCore<GrupoUnidadMedidaArticulo, int>
    {
        public GrupoUnidadMedidaArticuloRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
