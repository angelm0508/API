using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IMovimientoInventarioDomain
    {
        Task<IEnumerable<MovimientoInventario>> ObtenerPorArticuloAsync(string codArticulo, string? almacen, DateTime? desde, DateTime? hasta);
    }
}
