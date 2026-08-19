using API.Application.DTO;
using API.Application.DTO.direccionSocioNegocio;

namespace API.Application.Interface
{
    public interface IDireccionSocioNegocioApplication
    {
        #region async methods
        Task<Respuesta<bool>> InsertarAsync(DireccionSocioNegocioCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(string codigo, DireccionSocioNegocioActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(string codigo);
        Task<Respuesta<DireccionSocioNegocioDTO>> ObtenerPorCodigoAsync(string codigo);
        Task<Respuesta<IEnumerable<DireccionSocioNegocioDTO>>> ObtenerAsync();
        Task<Respuesta<IEnumerable<DireccionSocioNegocioDTO>>> ObtenerContengaCodigoAsync(string codigo);
        #endregion
    }
}
