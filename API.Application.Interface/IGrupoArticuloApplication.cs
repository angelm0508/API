using API.Application.DTO;
using API.Application.DTO.articulo.grupo_articulo;

namespace API.Application.Interface
{
    public interface IGrupoArticuloApplication
    {

        #region async methods
        Task<Respuesta<int>> InsertarAsync(GrupoArticuloCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, GrupoArticuloActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<GrupoArticuloDTO>> ObtenerAsync(int id);
        Task<Respuesta<GrupoArticuloDTO>> ObtenerAsync(string name);
        Task<Respuesta<IEnumerable<GrupoArticuloDTO>>> ObtenerContengaNombreAsync(string name);
        Task<Respuesta<IEnumerable<GrupoArticuloDTO>>> ObtenerTodoAsync();
        // Task<Respuesta<PagedList<GrupoArticuloDTO>>> GetAllWithPagingAsync(PaginationParametersDTO paginationParametersDTO);
        #endregion
    }
}
