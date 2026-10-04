using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface INotaCreditoCompraDomain
    {
        #region async methods
        Task<int> InsertarAsync(NotaCreditoCompra obj, IEnumerable<NotaCreditoCompraDetalle> lineas);
        Task<bool> ActualizarAsync(int id, NotaCreditoCompra obj);
        Task<bool> EliminarAsync(int id);
        Task<NotaCreditoCompra> ObtenerAsync(int id);
        Task<IQueryable<NotaCreditoCompra>> ObtenerTodoAsync();
        Task<IEnumerable<NotaCreditoCompraDetalle>> ObtenerLineasAsync(int id);
        Task<IEnumerable<FacturaCompraAcreditable>> ObtenerFacturasCompraBaseAsync(string codigoSn);
        Task<FacturaCompraBaseCredito> ObtenerFacturaCompraBaseAsync(int entry);
        #endregion
    }
}
