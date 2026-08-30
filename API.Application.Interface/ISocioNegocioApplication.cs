using API.Application.DTO;
using API.Application.DTO.socioNegocio;

namespace API.Application.Interface
{
    public interface ISocioNegocioApplication
    {
        #region async methods
        Task<Respuesta<string>> InsertarAsync(SocioNegocioCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(string codigo, SocioNegocioActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(string codigo);
        Task<Respuesta<SocioNegocioDTO>> ObtenerPorNombreAsync(string nombre);
        Task<Respuesta<SocioNegocioDTO>> ObtenerPorCodigoAsync(string codigo);
        Task<Respuesta<IEnumerable<SocioNegocioDTO>>> ObtenerAsync(string? tipo = null);
        Task<Respuesta<IEnumerable<SocioNegocioDTO>>> ObtenerContengaNombreAsync(string nombre, string? tipo = null);
        Task<Respuesta<IEnumerable<SocioNegocioDTO>>> ObtenerContengaCodigoAsync(string codigo);
        #endregion
    }
}
