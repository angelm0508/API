using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class ListadoPrecioRepositorio : RepositorioGenericoEfCore<ListadoPrecio, int>
    {
        public ListadoPrecioRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
