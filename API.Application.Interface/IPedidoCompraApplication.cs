using API.Application.DTO;
using API.Application.DTO.pedidoCompra;

namespace API.Application.Interface
{
    public interface IPedidoCompraApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(PedidoCompraCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, PedidoCompraActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<PedidoCompraDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<PedidoCompraDTO>>> ObtenerTodoAsync();
        #endregion
    }
}
