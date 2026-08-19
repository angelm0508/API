using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IPaisDomain
    {
        #region async methods
        Task<bool> InsertarAsync(Pai obj);
        Task<bool> ActualizarAsync(string codigo, Pai obj);
        Task<bool> EliminarAsync(string codigo);
        Task<Pai> ObtenerPorCodigoAsync(string codigo);
        Task<Pai> ObtenerPorNombreAsync(string nombre);
        Task<IQueryable<Pai>> ObtenerTodoAsync();
        Task<IEnumerable<Pai>> ObtenerContengaNombreAsync(string nombre);
        Task<IEnumerable<Pai>> ObtenerContengaCodigoAsync(string codigo);
        #endregion
    }
}
