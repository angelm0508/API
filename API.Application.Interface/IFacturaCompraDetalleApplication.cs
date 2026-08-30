using API.Application.DTO;
using API.Application.DTO.facturaCompra;

namespace API.Application.Interface
{
    public interface IFacturaCompraDetalleApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(FacturaCompraDetalleCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, FacturaCompraDetalleActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea);
        Task<Respuesta<FacturaCompraDetalleDTO>> ObtenerAsync(int entry, int noLinea);
        Task<Respuesta<IEnumerable<FacturaCompraDetalleDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<FacturaCompraDetalleDTO>>> ObtenerPorFacturaCompraAsync(int entry);
        #endregion
    }
}
