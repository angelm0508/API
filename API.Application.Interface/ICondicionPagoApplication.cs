using API.Application.DTO;
using API.Application.DTO.condicionPago;

namespace API.Application.Interface
{
    public interface ICondicionPagoApplication
    {
        #region async methods
        Task<Respuesta<bool>> InsertarAsync(CondicionPagoCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(string codigo, CondicionPagoActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(string codigo);
        Task<Respuesta<CondicionPagoDTO>> ObtenerPorCodigoAsync(string codigo);
        Task<Respuesta<IEnumerable<CondicionPagoDTO>>> ObtenerAsync();
        #endregion
    }
}
