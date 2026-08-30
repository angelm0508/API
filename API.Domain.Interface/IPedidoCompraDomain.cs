using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IPedidoCompraDomain
    {
        #region async methods
        Task<int> InsertarAsync(PedidoCompra obj);
        Task<bool> ActualizarAsync(int id, PedidoCompra obj);
        Task<bool> EliminarAsync(int id);
        Task<PedidoCompra> ObtenerAsync(int id);
        Task<IQueryable<PedidoCompra>> ObtenerTodoAsync();
        #endregion
    }
}
