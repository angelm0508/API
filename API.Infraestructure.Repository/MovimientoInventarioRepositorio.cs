using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class MovimientoInventarioRepositorio : RepositorioGenericoEfCore<MovimientoInventario, int>
    {
        public MovimientoInventarioRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
