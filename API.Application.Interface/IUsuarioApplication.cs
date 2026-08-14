using API.Application.DTO;
using API.Application.DTO.usuario.usuario;

namespace API.Application.Interface
{
    public interface IUsuarioApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(UsuarioCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, UsuarioActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<UsuarioDTO>> ObtenerAsync(int id);
        Task<Respuesta<UsuarioDTO>> ObtenerAsync(string codigo);
        Task<Respuesta<IEnumerable<UsuarioDTO>>> ObtenerContengaNombreAsync(string name);
        Task<Respuesta<IEnumerable<UsuarioDTO>>> ObtenerTodoAsync();
        #endregion
    }
}
