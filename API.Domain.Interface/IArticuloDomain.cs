using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IArticuloDomain
    {
        #region async methods
        Task<string> InsertarAsync(Articulo obj);
        Task<bool> ActualizarAsync(string sku, Articulo obj);
        Task<bool> EliminarAsync(string sku);
        Task<Articulo> ObtenerPorCodigoAsync(string sku);
        Task<Articulo> ObtenerPorNombreAsync(string name);
        Task<IQueryable<Articulo>> ObtenerTodoAsync();
        Task<IEnumerable<Articulo>> ObtenerContengaNombreAsync(string name);
        Task<IEnumerable<Articulo>> ObtenerContengaCodigoAsync(string sku);
        Task<IQueryable<Articulo>> ObtenerConPaginacionAsync();
        #endregion
    }
}
