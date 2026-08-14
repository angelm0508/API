using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IGrupoMedidaDetArticuloDomain
    {
        #region async methods
        Task<int> InsertarAsync(GrupoMedidaDetArticulo obj);
        Task<bool> ActualizarAsync(int id, GrupoMedidaDetArticulo obj);
        Task<bool> EliminarAsync(int id);
        Task<GrupoMedidaDetArticulo> ObtenerAsync(int id);
        Task<IQueryable<GrupoMedidaDetArticulo>> ObtenerTodoAsync();
        #endregion
    }
}
