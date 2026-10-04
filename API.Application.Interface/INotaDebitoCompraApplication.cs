using API.Application.DTO;
using API.Application.DTO.notaDebitoCompra;

namespace API.Application.Interface
{
    public interface INotaDebitoCompraApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(NotaDebitoCompraCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, NotaDebitoCompraActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<NotaDebitoCompraDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<NotaDebitoCompraDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<NotaDebitoCompraDetalleDTO>>> ObtenerLineasAsync(int id);
        Task<Respuesta<IEnumerable<NotaDebitoCompraFacturaBaseResumenDTO>>> ObtenerFacturasCompraBaseAsync(string codigoSn);
        Task<Respuesta<NotaDebitoCompraFacturaBaseDTO>> ObtenerFacturaCompraBaseAsync(int entry);
        #endregion
    }
}
