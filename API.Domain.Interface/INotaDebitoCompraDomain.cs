using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface INotaDebitoCompraDomain
    {
        #region async methods
        Task<int> InsertarAsync(NotaDebitoCompra obj, IEnumerable<NotaDebitoCompraDetalle> lineas);
        Task<bool> ActualizarAsync(int id, NotaDebitoCompra obj);
        Task<bool> EliminarAsync(int id);
        Task<NotaDebitoCompra> ObtenerAsync(int id);
        Task<IQueryable<NotaDebitoCompra>> ObtenerTodoAsync();
        Task<IEnumerable<NotaDebitoCompraDetalle>> ObtenerLineasAsync(int id);
        Task<IEnumerable<FacturaCompra>> ObtenerFacturasCompraBaseAsync(string codigoSn);
        Task<FacturaCompraBase> ObtenerFacturaCompraBaseAsync(int entry);
        #endregion
    }
}
