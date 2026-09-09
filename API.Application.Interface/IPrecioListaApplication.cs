using API.Application.DTO;
using API.Application.DTO.precio;

namespace API.Application.Interface
{
    public interface IPrecioListaApplication
    {
        Task<Respuesta<PrecioResueltoDTO>> ResolverAsync(string codigoArticulo, int? listaPrecioEntry);
    }
}
