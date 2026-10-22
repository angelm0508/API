using API.Application.DTO;
using API.Application.DTO.ordenServicio;

namespace API.Application.Interface
{
    public interface IOrdenServicioDetalleApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(OrdenServicioDetalleCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, OrdenServicioDetalleActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea);
        #endregion
    }
}
