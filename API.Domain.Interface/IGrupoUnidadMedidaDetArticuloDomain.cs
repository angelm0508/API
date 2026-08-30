using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IGrupoUnidadMedidaDetArticuloDomain
    {
        #region async methods
        Task<int> InsertarAsync(GrupoUnidadMedidaDetArticulo obj);
        Task<bool> ActualizarAsync(int grpMedidaEntry, int numLinea, GrupoUnidadMedidaDetArticulo obj);
        Task<bool> EliminarAsync(int grpMedidaEntry, int numLinea);
        Task<GrupoUnidadMedidaDetArticulo> ObtenerAsync(int grpMedidaEntry, int numLinea);
        Task<IQueryable<GrupoUnidadMedidaDetArticulo>> ObtenerTodoAsync();
        Task<IEnumerable<GrupoUnidadMedidaDetArticulo>> ObtenerPorGrupoAsync(int grpMedidaEntry);
        #endregion
    }
}
