using API.Application.DTO;
using API.Application.DTO.entregaCompra;

namespace API.Application.Interface
{
    public interface IEntregaCompraDetalleApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(EntregaCompraDetalleCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, EntregaCompraDetalleActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea);
        Task<Respuesta<EntregaCompraDetalleDTO>> ObtenerAsync(int entry, int noLinea);
        Task<Respuesta<IEnumerable<EntregaCompraDetalleDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<EntregaCompraDetalleDTO>>> ObtenerPorEntregaCompraAsync(int entry);
        #endregion
    }
}
