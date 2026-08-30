using API.Application.DTO;
using API.Application.DTO.pedido;

namespace API.Application.Interface
{
    public interface IPedidoDetalleApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(PedidoDetalleCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, PedidoDetalleActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea);
        Task<Respuesta<PedidoDetalleDTO>> ObtenerAsync(int entry, int noLinea);
        Task<Respuesta<IEnumerable<PedidoDetalleDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<PedidoDetalleDTO>>> ObtenerPorPedidoAsync(int entry);
        #endregion
    }
}
