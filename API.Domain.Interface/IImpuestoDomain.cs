using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IImpuestoDomain
    {
        #region async methods
        Task<bool> InsertarAsync(Impuesto obj);
        Task<bool> ActualizarAsync(string codigo, Impuesto obj);
        Task<bool> EliminarAsync(string codigo);
        Task<Impuesto> ObtenerPorCodigoAsync(string codigo);
        Task<IQueryable<Impuesto>> ObtenerTodoAsync();
        Task<IEnumerable<Impuesto>> ObtenerContengaNombreAsync(string nombre);
        #endregion
    }
}
