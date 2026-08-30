using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IPedidoCompraDetalleDomain
    {
        #region async methods
        Task<int> InsertarAsync(PedidoCompraDetalle obj);
        Task<bool> ActualizarAsync(int entry, int noLinea, PedidoCompraDetalle obj);
        Task<bool> EliminarAsync(int entry, int noLinea);
        Task<PedidoCompraDetalle> ObtenerAsync(int entry, int noLinea);
        Task<IQueryable<PedidoCompraDetalle>> ObtenerTodoAsync();
        Task<IEnumerable<PedidoCompraDetalle>> ObtenerPorPedidoCompraAsync(int entry);
        #endregion
    }
}
