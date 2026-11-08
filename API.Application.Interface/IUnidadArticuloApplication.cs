using API.Application.DTO;
using API.Application.DTO.articulo.unidades;

namespace API.Application.Interface
{
    public interface IUnidadArticuloApplication
    {
        Task<Respuesta<UnidadesArticuloDTO>> ObtenerAsync(string codigoArticulo);
    }
}
