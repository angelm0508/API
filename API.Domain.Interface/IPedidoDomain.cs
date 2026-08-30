using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IPedidoDomain
    {
        #region async methods
        Task<int> InsertarAsync(Pedido obj);
        Task<bool> ActualizarAsync(int id, Pedido obj);
        Task<bool> EliminarAsync(int id);
        Task<Pedido> ObtenerAsync(int id);
        Task<IQueryable<Pedido>> ObtenerTodoAsync();
        #endregion
    }
}
