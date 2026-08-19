using API.Application.DTO;
using API.Application.DTO.municipio;

namespace API.Application.Interface
{
    public interface IMunicipioApplication
    {
        #region async methods
        Task<Respuesta<bool>> InsertarAsync(MunicipioCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(string codigo, MunicipioActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(string codigo);
        Task<Respuesta<MunicipioDTO>> ObtenerPorNombreAsync(string nombre);
        Task<Respuesta<MunicipioDTO>> ObtenerPorCodigoAsync(string codigo);
        Task<Respuesta<IEnumerable<MunicipioDTO>>> ObtenerAsync();
        Task<Respuesta<IEnumerable<MunicipioDTO>>> ObtenerContengaNombreAsync(string nombre);
        Task<Respuesta<IEnumerable<MunicipioDTO>>> ObtenerContengaCodigoAsync(string codigo);
        #endregion
    }
}
