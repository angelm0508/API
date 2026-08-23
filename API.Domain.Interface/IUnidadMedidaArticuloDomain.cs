using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IUnidadMedidaArticuloDomain
    {
        #region async methods
        Task<int> InsertarAsync(UnidadMedidaArticulo obj);
        Task<bool> ActualizarAsync(int id, UnidadMedidaArticulo obj);
        Task<bool> EliminarAsync(int id);
        Task<UnidadMedidaArticulo> ObtenerAsync(int id);
        Task<UnidadMedidaArticulo> ObtenerAsync(string codigo);
        Task<IQueryable<UnidadMedidaArticulo>> ObtenerTodoAsync();
        Task<IEnumerable<UnidadMedidaArticulo>> ObtenerContengaNombreAsync(string name);
        #endregion
    }
}
