using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IMonedaDomain
    {
        #region async methods
        Task<bool> InsertarAsync(Monedum obj);
        Task<bool> ActualizarAsync(string codigo, Monedum obj);
        Task<bool> EliminarAsync(string codigo);
        Task<Monedum> ObtenerPorCodigoAsync(string codigo);
        Task<Monedum> ObtenerPorNombreAsync(string nombre);
        Task<IQueryable<Monedum>> ObtenerTodoAsync();
        Task<IEnumerable<Monedum>> ObtenerContengaNombreAsync(string nombre);
        Task<IEnumerable<Monedum>> ObtenerContengaCodigoAsync(string codigo);
        #endregion
    }
}
