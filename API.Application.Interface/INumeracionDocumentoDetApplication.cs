using API.Application.DTO;
using API.Application.DTO.numeracion.numeracion_documento_det;

namespace API.Application.Interface
{
    public interface INumeracionDocumentoDetApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(NumeracionDocumentoDetCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int serie, NumeracionDocumentoDetActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int serie);
        Task<Respuesta<NumeracionDocumentoDetDTO>> ObtenerAsync(int serie);
        Task<Respuesta<IEnumerable<NumeracionDocumentoDetDTO>>> ObtenerPorDocumentoAsync(string codigoObj);
        Task<Respuesta<IEnumerable<NumeracionDocumentoDetDTO>>> ObtenerTodoAsync();
        Task<Respuesta<string>> GenerarCodigoAsync(int serie);
        #endregion
    }
}
