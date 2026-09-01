using API.Application.DTO;
using API.Application.DTO.entradaMercancia;

namespace API.Application.Interface
{
    public interface IEntradaMercanciaDetalleApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(EntradaMercanciaDetalleCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, EntradaMercanciaDetalleActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea);
        Task<Respuesta<EntradaMercanciaDetalleDTO>> ObtenerAsync(int entry, int noLinea);
        Task<Respuesta<IEnumerable<EntradaMercanciaDetalleDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<EntradaMercanciaDetalleDTO>>> ObtenerPorEntradaMercanciaAsync(int entry);
        #endregion
    }
}
