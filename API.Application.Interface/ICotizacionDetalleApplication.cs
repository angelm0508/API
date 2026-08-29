using API.Application.DTO;
using API.Application.DTO.cotizacion;

namespace API.Application.Interface
{
    public interface ICotizacionDetalleApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(CotizacionDetalleCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, CotizacionDetalleActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea);
        Task<Respuesta<CotizacionDetalleDTO>> ObtenerAsync(int entry, int noLinea);
        Task<Respuesta<IEnumerable<CotizacionDetalleDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<CotizacionDetalleDTO>>> ObtenerPorCotizacionAsync(int entry);
        #endregion
    }
}
