using API.Application.DTO;
using API.Application.DTO.entrega;

namespace API.Application.Interface
{
    public interface IEntregaApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(EntregaCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, EntregaActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<EntregaDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<EntregaDTO>>> ObtenerTodoAsync();
        #endregion
    }
}
