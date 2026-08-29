using API.Application.DTO;
using API.Application.DTO.entrega;

namespace API.Application.Interface
{
    public interface IEntregaDetalleApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(EntregaDetalleCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, EntregaDetalleActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea);
        Task<Respuesta<EntregaDetalleDTO>> ObtenerAsync(int entry, int noLinea);
        Task<Respuesta<IEnumerable<EntregaDetalleDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<EntregaDetalleDTO>>> ObtenerPorEntregaAsync(int entry);
        #endregion
    }
}
