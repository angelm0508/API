using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface INumeracionDocumentoDomain
    {
        #region async methods
        Task<bool> InsertarAsync(NumeracionDocumento obj);
        Task<bool> ActualizarAsync(string codigo, NumeracionDocumento obj);
        Task<bool> EliminarAsync(string codigo);
        Task<NumeracionDocumento> ObtenerPorCodigoAsync(string codigo);
        Task<IQueryable<NumeracionDocumento>> ObtenerTodoAsync();
        Task<IEnumerable<NumeracionDocumento>> ObtenerContengaCodigoAsync(string codigo);
        #endregion
    }
}
