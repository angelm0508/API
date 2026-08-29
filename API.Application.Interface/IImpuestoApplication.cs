using API.Application.DTO;
using API.Application.DTO.impuesto;

namespace API.Application.Interface
{
    public interface IImpuestoApplication
    {
        #region async methods
        Task<Respuesta<bool>> InsertarAsync(ImpuestoCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(string codigo, ImpuestoActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(string codigo);
        Task<Respuesta<ImpuestoDTO>> ObtenerPorCodigoAsync(string codigo);
        Task<Respuesta<IEnumerable<ImpuestoDTO>>> ObtenerAsync();
        #endregion
    }
}
