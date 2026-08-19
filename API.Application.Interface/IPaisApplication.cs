using API.Application.DTO;
using API.Application.DTO.pais;

namespace API.Application.Interface
{
    public interface IPaisApplication
    {
        #region async methods
        Task<Respuesta<bool>> InsertarAsync(PaisCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(string codigo, PaisActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(string codigo);
        Task<Respuesta<PaisDTO>> ObtenerPorNombreAsync(string nombre);
        Task<Respuesta<PaisDTO>> ObtenerPorCodigoAsync(string codigo);
        Task<Respuesta<IEnumerable<PaisDTO>>> ObtenerAsync();
        Task<Respuesta<IEnumerable<PaisDTO>>> ObtenerContengaNombreAsync(string nombre);
        Task<Respuesta<IEnumerable<PaisDTO>>> ObtenerContengaCodigoAsync(string codigo);
        #endregion
    }
}
