using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface INumeracionDocumentoDetDomain
    {
        #region async methods
        Task<int> InsertarAsync(NumeracionDocumentoDet obj);
        Task<bool> ActualizarAsync(int serie, NumeracionDocumentoDet obj);
        Task<bool> EliminarAsync(int serie);
        Task<NumeracionDocumentoDet?> ObtenerAsync(int serie);
        Task<IEnumerable<NumeracionDocumentoDet>> ObtenerPorDocumentoAsync(string codigoObj);
        Task<IQueryable<NumeracionDocumentoDet>> ObtenerTodoAsync();
        Task<string> GenerarCodigoAsync(int serie);
        #endregion
    }
}
