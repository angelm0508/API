using API.Application.DTO;
using API.Application.DTO.salidaMercancia;

namespace API.Application.Interface
{
    public interface ISalidaMercanciaApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(SalidaMercanciaCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, SalidaMercanciaActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<SalidaMercanciaDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<SalidaMercanciaDTO>>> ObtenerTodoAsync();
        #endregion
    }
}
