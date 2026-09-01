using API.Application.DTO;
using API.Application.DTO.salidaMercancia;

namespace API.Application.Interface
{
    public interface ISalidaMercanciaDetalleApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(SalidaMercanciaDetalleCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, SalidaMercanciaDetalleActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea);
        Task<Respuesta<SalidaMercanciaDetalleDTO>> ObtenerAsync(int entry, int noLinea);
        Task<Respuesta<IEnumerable<SalidaMercanciaDetalleDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<SalidaMercanciaDetalleDTO>>> ObtenerPorSalidaMercanciaAsync(int entry);
        #endregion
    }
}
