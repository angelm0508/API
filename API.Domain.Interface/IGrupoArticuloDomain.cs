using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IGrupoArticuloDomain
    {
        #region async methods
        Task<int> InsertarAsync(GrupoArticulo obj);
        Task<bool> ActualizarAsync(int id, GrupoArticulo obj);
        Task<bool> EliminarAsync(int id);
        Task<GrupoArticulo> ObtenerAsync(int id);
        Task<GrupoArticulo> ObtenerAsync(string name);
        Task<IQueryable<GrupoArticulo>> ObtenerTodoAsync();
        //Task<IQueryable<GrupoArticulo>> GetAllWithPagingAsync();
        Task<IEnumerable<GrupoArticulo>> ObtenerContengaNombreAsync(string name);
        #endregion
    }
}
