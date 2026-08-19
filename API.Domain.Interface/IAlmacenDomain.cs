using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IAlmacenDomain
    {
        #region async methods
        Task<bool> InsertarAsync(Almacen obj);
        Task<bool> ActualizarAsync(string codigo, Almacen obj);
        Task<bool> EliminarAsync(string codigo);
        Task<Almacen> ObtenerPorCodigoAsync(string codigo);
        Task<Almacen> ObtenerPorNombreAsync(string nombre);
        Task<IQueryable<Almacen>> ObtenerTodoAsync();
        Task<IEnumerable<Almacen>> ObtenerContengaNombreAsync(string nombre);
        Task<IEnumerable<Almacen>> ObtenerContengaCodigoAsync(string codigo);
        #endregion
    }
}
