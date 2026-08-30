using API.Application.DTO;
using API.Application.DTO.entregaCompra;

namespace API.Application.Interface
{
    public interface IEntregaCompraApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(EntregaCompraCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, EntregaCompraActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<EntregaCompraDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<EntregaCompraDTO>>> ObtenerTodoAsync();
        #endregion
    }
}
