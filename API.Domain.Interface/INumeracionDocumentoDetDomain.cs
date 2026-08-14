using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface INumeracionDocumentoDetDomain
    {
        #region async methods
        Task<string> InsertarAsync(NumeracionDocumentoDet obj);
        Task<bool> ActualizarAsync(string codigoObj, NumeracionDocumentoDet obj);
        Task<bool> EliminarAsync(string codigoObj);
        Task<NumeracionDocumentoDet> ObtenerAsync(string codigoObj);
        Task<IQueryable<NumeracionDocumentoDet>> ObtenerTodoAsync();
        #endregion
    }
}
