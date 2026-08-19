using API.Application.DTO;
using API.Application.DTO.numeracionDocumento;

namespace API.Application.Interface
{
    public interface INumeracionDocumentoApplication
    {
        #region async methods
        Task<Respuesta<bool>> InsertarAsync(NumeracionDocumentoCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(string codigo, NumeracionDocumentoActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(string codigo);
        Task<Respuesta<NumeracionDocumentoDTO>> ObtenerPorCodigoAsync(string codigo);
        Task<Respuesta<IEnumerable<NumeracionDocumentoDTO>>> ObtenerAsync();
        Task<Respuesta<IEnumerable<NumeracionDocumentoDTO>>> ObtenerContengaCodigoAsync(string codigo);
        #endregion
    }
}
