using API.Application.DTO;
using API.Application.DTO.cotizacion;

namespace API.Application.Interface
{
    public interface ICotizacionApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(CotizacionCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, CotizacionActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<CotizacionDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<CotizacionDTO>>> ObtenerTodoAsync();
        #endregion
    }
}
