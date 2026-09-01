using API.Application.DTO;
using API.Application.DTO.entradaMercancia;

namespace API.Application.Interface
{
    public interface IEntradaMercanciaApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(EntradaMercanciaCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, EntradaMercanciaActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<EntradaMercanciaDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<EntradaMercanciaDTO>>> ObtenerTodoAsync();
        #endregion
    }
}
