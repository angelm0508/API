using API.Application.DTO;
using API.Application.DTO.factura;

namespace API.Application.Interface
{
    public interface IFacturaDetalleApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(FacturaDetalleCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, FacturaDetalleActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea);
        Task<Respuesta<FacturaDetalleDTO>> ObtenerAsync(int entry, int noLinea);
        Task<Respuesta<IEnumerable<FacturaDetalleDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<FacturaDetalleDTO>>> ObtenerPorFacturaAsync(int entry);
        #endregion
    }
}
