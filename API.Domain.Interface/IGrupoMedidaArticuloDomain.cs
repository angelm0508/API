using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IGrupoMedidaArticuloDomain
    {
        #region async methods
        Task<int> InsertarAsync(GrupoMedidaArticulo obj);
        Task<bool> ActualizarAsync(int id, GrupoMedidaArticulo obj);
        Task<bool> EliminarAsync(int id);
        Task<GrupoMedidaArticulo> ObtenerAsync(int id);
        Task<GrupoMedidaArticulo> ObtenerAsync(string name);
        Task<IQueryable<GrupoMedidaArticulo>> ObtenerTodoAsync();
        Task<IEnumerable<GrupoMedidaArticulo>> ObtenerContengaNombreAsync(string name);
        #endregion
    }
}
