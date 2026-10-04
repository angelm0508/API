using API.Application.DTO;
using API.Application.DTO.notaCredito;

namespace API.Application.Interface
{
    public interface INotaCreditoApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(NotaCreditoCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, NotaCreditoActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<NotaCreditoDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<NotaCreditoDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<NotaCreditoDetalleDTO>>> ObtenerLineasAsync(int id);
        Task<Respuesta<IEnumerable<NotaCreditoFacturaBaseResumenDTO>>> ObtenerFacturasBaseAsync(string codigoSn);
        Task<Respuesta<NotaCreditoFacturaBaseDTO>> ObtenerFacturaBaseAsync(int entry);
        #endregion
    }
}
