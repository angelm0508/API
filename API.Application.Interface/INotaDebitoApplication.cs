using API.Application.DTO;
using API.Application.DTO.notaDebito;

namespace API.Application.Interface
{
    public interface INotaDebitoApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(NotaDebitoCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, NotaDebitoActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<NotaDebitoDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<NotaDebitoDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<NotaDebitoDetalleDTO>>> ObtenerLineasAsync(int id);
        Task<Respuesta<IEnumerable<NotaDebitoFacturaBaseResumenDTO>>> ObtenerFacturasBaseAsync(string codigoSn);
        Task<Respuesta<NotaDebitoFacturaBaseDTO>> ObtenerFacturaBaseAsync(int entry);
        #endregion
    }
}
