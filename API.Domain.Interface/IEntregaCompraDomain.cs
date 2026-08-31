using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IEntregaCompraDomain
    {
        #region async methods
        Task<int> InsertarAsync(EntregaCompra obj, IEnumerable<EntregaCompraDetalle> lineas);
        Task<bool> ActualizarAsync(int id, EntregaCompra obj);
        Task<bool> EliminarAsync(int id);
        Task<EntregaCompra> ObtenerAsync(int id);
        Task<IQueryable<EntregaCompra>> ObtenerTodoAsync();
        #endregion
    }
}
