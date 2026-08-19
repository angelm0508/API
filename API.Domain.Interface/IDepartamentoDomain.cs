using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IDepartamentoDomain
    {
        #region async methods
        Task<bool> InsertarAsync(Departamento obj);
        Task<bool> ActualizarAsync(string codigo, Departamento obj);
        Task<bool> EliminarAsync(string codigo);
        Task<Departamento> ObtenerPorCodigoAsync(string codigo);
        Task<Departamento> ObtenerPorNombreAsync(string nombre);
        Task<IQueryable<Departamento>> ObtenerTodoAsync();
        Task<IEnumerable<Departamento>> ObtenerContengaNombreAsync(string nombre);
        Task<IEnumerable<Departamento>> ObtenerContengaCodigoAsync(string codigo);
        #endregion
    }
}
