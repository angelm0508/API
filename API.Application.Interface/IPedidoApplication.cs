using API.Application.DTO;
using API.Application.DTO.pedido;

namespace API.Application.Interface
{
    public interface IPedidoApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(PedidoCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, PedidoActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<PedidoDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<PedidoDTO>>> ObtenerTodoAsync();
        #endregion
    }
}
