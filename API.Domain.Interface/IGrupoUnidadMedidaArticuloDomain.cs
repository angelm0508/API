using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IGrupoUnidadMedidaArticuloDomain
    {
        #region async methods
        Task<int> InsertarAsync(GrupoUnidadMedidaArticulo obj);
        Task<bool> ActualizarAsync(int id, GrupoUnidadMedidaArticulo obj);
        Task<bool> EliminarAsync(int id);
        Task<GrupoUnidadMedidaArticulo> ObtenerAsync(int id);
        Task<GrupoUnidadMedidaArticulo> ObtenerAsync(string name);
        Task<IQueryable<GrupoUnidadMedidaArticulo>> ObtenerTodoAsync();
        Task<IEnumerable<GrupoUnidadMedidaArticulo>> ObtenerContengaNombreAsync(string name);
        #endregion
    }
}
