using API.Application.DTO;
using API.Application.DTO.factura;

namespace API.Application.Interface
{
    public interface IFacturaApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(FacturaCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, FacturaActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<FacturaDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<FacturaDTO>>> ObtenerTodoAsync();
        #endregion
    }
}
