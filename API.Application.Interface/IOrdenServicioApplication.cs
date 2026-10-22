using API.Application.DTO;
using API.Application.DTO.ordenServicio;

namespace API.Application.Interface
{
    public interface IOrdenServicioApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(OrdenServicioCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, OrdenServicioActualizarDTO obj);
        Task<Respuesta<bool>> CambiarEstadoAsync(int id, string estado);
        Task<Respuesta<bool>> CancelarAsync(int id);
        Task<Respuesta<OrdenServicioDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<OrdenServicioDTO>>> ObtenerTodoAsync(string? estado);
        Task<Respuesta<IEnumerable<OrdenServicioDetalleDTO>>> ObtenerLineasAsync(int id);
        #endregion
    }
}
