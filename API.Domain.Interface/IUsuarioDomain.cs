using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IUsuarioDomain
    {
        #region async methods
        Task<int> InsertarAsync(Usuario obj);
        Task<bool> ActualizarAsync(int id, Usuario obj);
        Task<bool> EliminarAsync(int id);
        Task<Usuario> ObtenerAsync(int id);
        Task<Usuario> ObtenerAsync(string codigo);
        Task<IQueryable<Usuario>> ObtenerTodoAsync();
        Task<IEnumerable<Usuario>> ObtenerContengaNombreAsync(string name);
        #endregion
    }
}
