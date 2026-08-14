using API.Application.DTO;
using API.Application.DTO.articulo.grupo_sn;

namespace API.Application.Interface
{
    public interface IGrupoSnApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(GrupoSnCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, GrupoSnActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<GrupoSnDTO>> ObtenerAsync(int id);
        Task<Respuesta<GrupoSnDTO>> ObtenerAsync(string name);
        Task<Respuesta<IEnumerable<GrupoSnDTO>>> ObtenerContengaNombreAsync(string name);
        Task<Respuesta<IEnumerable<GrupoSnDTO>>> ObtenerTodoAsync();
        #endregion
    }
}
