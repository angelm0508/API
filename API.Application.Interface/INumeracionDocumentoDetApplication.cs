using API.Application.DTO;
using API.Application.DTO.numeracion.numeracion_documento_det;

namespace API.Application.Interface
{
    public interface INumeracionDocumentoDetApplication
    {
        #region async methods
        Task<Respuesta<string>> InsertarAsync(NumeracionDocumentoDetCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(string codigoObj, NumeracionDocumentoDetActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(string codigoObj);
        Task<Respuesta<NumeracionDocumentoDetDTO>> ObtenerAsync(string codigoObj);
        Task<Respuesta<IEnumerable<NumeracionDocumentoDetDTO>>> ObtenerTodoAsync();
        #endregion
    }
}
