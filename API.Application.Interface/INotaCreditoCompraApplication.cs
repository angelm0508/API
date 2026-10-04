using API.Application.DTO;
using API.Application.DTO.notaCreditoCompra;

namespace API.Application.Interface
{
    public interface INotaCreditoCompraApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(NotaCreditoCompraCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, NotaCreditoCompraActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<NotaCreditoCompraDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<NotaCreditoCompraDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<NotaCreditoCompraDetalleDTO>>> ObtenerLineasAsync(int id);
        Task<Respuesta<IEnumerable<NotaCreditoCompraFacturaBaseResumenDTO>>> ObtenerFacturasCompraBaseAsync(string codigoSn);
        Task<Respuesta<NotaCreditoCompraFacturaBaseDTO>> ObtenerFacturaCompraBaseAsync(int entry);
        #endregion
    }
}
