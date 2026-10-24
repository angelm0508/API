using API.Application.DTO;
using API.Application.DTO.formaPago;

namespace API.Application.Interface
{
    public interface IFormaPagoApplication
    {
        #region async methods
        Task<Respuesta<bool>> InsertarAsync(FormaPagoCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(string codigo, FormaPagoActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(string codigo);
        Task<Respuesta<FormaPagoDTO>> ObtenerPorCodigoAsync(string codigo);
        Task<Respuesta<IEnumerable<FormaPagoDTO>>> ObtenerAsync();
        #endregion
    }
}
