using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IEntregaDetalleDomain
    {
        #region async methods
        Task<int> InsertarAsync(EntregaDetalle obj);
        Task<bool> ActualizarAsync(int entry, int noLinea, EntregaDetalle obj);
        Task<bool> EliminarAsync(int entry, int noLinea);
        Task<EntregaDetalle> ObtenerAsync(int entry, int noLinea);
        Task<IQueryable<EntregaDetalle>> ObtenerTodoAsync();
        Task<IEnumerable<EntregaDetalle>> ObtenerPorEntregaAsync(int entry);
        #endregion
    }
}
