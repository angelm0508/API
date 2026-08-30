using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IFacturaCompraDomain
    {
        #region async methods
        Task<int> InsertarAsync(FacturaCompra obj);
        Task<bool> ActualizarAsync(int id, FacturaCompra obj);
        Task<bool> EliminarAsync(int id);
        Task<FacturaCompra> ObtenerAsync(int id);
        Task<IQueryable<FacturaCompra>> ObtenerTodoAsync();
        #endregion
    }
}
