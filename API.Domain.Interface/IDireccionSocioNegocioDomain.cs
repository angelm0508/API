using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IDireccionSocioNegocioDomain
    {
        #region async methods
        Task<bool> InsertarAsync(DireccionSocioNegocio obj);
        Task<bool> ActualizarAsync(string codigo, DireccionSocioNegocio obj);
        Task<bool> EliminarAsync(string codigo);
        Task<DireccionSocioNegocio> ObtenerPorCodigoAsync(string codigo);
        Task<IQueryable<DireccionSocioNegocio>> ObtenerTodoAsync();
        Task<IEnumerable<DireccionSocioNegocio>> ObtenerContengaCodigoAsync(string codigo);
        #endregion
    }
}
