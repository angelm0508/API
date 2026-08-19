using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IMunicipioDomain
    {
        #region async methods
        Task<bool> InsertarAsync(Municipio obj);
        Task<bool> ActualizarAsync(string codigo, Municipio obj);
        Task<bool> EliminarAsync(string codigo);
        Task<Municipio> ObtenerPorCodigoAsync(string codigo);
        Task<Municipio> ObtenerPorNombreAsync(string nombre);
        Task<IQueryable<Municipio>> ObtenerTodoAsync();
        Task<IEnumerable<Municipio>> ObtenerContengaNombreAsync(string nombre);
        Task<IEnumerable<Municipio>> ObtenerContengaCodigoAsync(string codigo);
        #endregion
    }
}
