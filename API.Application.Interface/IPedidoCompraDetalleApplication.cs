using API.Application.DTO;
using API.Application.DTO.pedidoCompra;

namespace API.Application.Interface
{
    public interface IPedidoCompraDetalleApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(PedidoCompraDetalleCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, PedidoCompraDetalleActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea);
        Task<Respuesta<PedidoCompraDetalleDTO>> ObtenerAsync(int entry, int noLinea);
        Task<Respuesta<IEnumerable<PedidoCompraDetalleDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<PedidoCompraDetalleDTO>>> ObtenerPorPedidoCompraAsync(int entry);
        #endregion
    }
}
