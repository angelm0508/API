using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IPedidoDetalleDomain
    {
        #region async methods
        Task<int> InsertarAsync(PedidoDetalle obj);
        Task<bool> ActualizarAsync(int entry, int noLinea, PedidoDetalle obj);
        Task<bool> EliminarAsync(int entry, int noLinea);
        Task<PedidoDetalle> ObtenerAsync(int entry, int noLinea);
        Task<IQueryable<PedidoDetalle>> ObtenerTodoAsync();
        Task<IEnumerable<PedidoDetalle>> ObtenerPorPedidoAsync(int entry);
        #endregion
    }
}
