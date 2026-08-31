using API.Application.DTO;
using API.Application.DTO.inventario;

namespace API.Application.Interface
{
    public interface IMovimientoInventarioApplication
    {
        Task<Respuesta<IEnumerable<MovimientoInventarioDTO>>> ObtenerPorArticuloAsync(string codArticulo, string? almacen, DateTime? desde, DateTime? hasta);
    }
}
