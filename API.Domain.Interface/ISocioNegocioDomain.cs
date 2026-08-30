using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface ISocioNegocioDomain
    {
        #region async methods
        Task<string> InsertarAsync(SocioNegocio obj);
        Task<bool> ActualizarAsync(string codigo, SocioNegocio obj);
        Task<bool> EliminarAsync(string codigo);
        Task<SocioNegocio> ObtenerPorCodigoAsync(string codigo);
        Task<SocioNegocio> ObtenerPorNombreAsync(string nombre);
        Task<IQueryable<SocioNegocio>> ObtenerTodoAsync();
        Task<IEnumerable<SocioNegocio>> ObtenerContengaNombreAsync(string nombre);
        Task<IEnumerable<SocioNegocio>> ObtenerContengaCodigoAsync(string codigo);
        #endregion
    }
}
