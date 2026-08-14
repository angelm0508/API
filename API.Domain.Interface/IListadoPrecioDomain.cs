using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IListadoPrecioDomain
    {
        #region async methods
        Task<int> InsertarAsync(ListadoPrecio obj);
        Task<bool> ActualizarAsync(int id, ListadoPrecio obj);
        Task<bool> EliminarAsync(int id);
        Task<ListadoPrecio> ObtenerAsync(int id);
        Task<ListadoPrecio> ObtenerAsync(string name);
        Task<IQueryable<ListadoPrecio>> ObtenerTodoAsync();
        Task<IEnumerable<ListadoPrecio>> ObtenerContengaNombreAsync(string name);
        #endregion
    }
}
