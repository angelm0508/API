using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IMedidaArticuloDomain
    {
        #region async methods
        Task<int> InsertarAsync(MedidaArticulo obj);
        Task<bool> ActualizarAsync(int id, MedidaArticulo obj);
        Task<bool> EliminarAsync(int id);
        Task<MedidaArticulo> ObtenerAsync(int id);
        Task<MedidaArticulo> ObtenerAsync(string codigo);
        Task<IQueryable<MedidaArticulo>> ObtenerTodoAsync();
        Task<IEnumerable<MedidaArticulo>> ObtenerContengaNombreAsync(string name);
        #endregion
    }
}
