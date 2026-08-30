using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IEntregaCompraDetalleDomain
    {
        #region async methods
        Task<int> InsertarAsync(EntregaCompraDetalle obj);
        Task<bool> ActualizarAsync(int entry, int noLinea, EntregaCompraDetalle obj);
        Task<bool> EliminarAsync(int entry, int noLinea);
        Task<EntregaCompraDetalle> ObtenerAsync(int entry, int noLinea);
        Task<IQueryable<EntregaCompraDetalle>> ObtenerTodoAsync();
        Task<IEnumerable<EntregaCompraDetalle>> ObtenerPorEntregaCompraAsync(int entry);
        #endregion
    }
}
