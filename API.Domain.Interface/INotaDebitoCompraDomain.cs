using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface INotaDebitoCompraDomain
    {
        #region async methods
        Task<int> InsertarAsync(FacturaCompra obj, IEnumerable<FacturaCompraDetalle> lineas);
        Task<bool> ActualizarAsync(int id, FacturaCompra obj);
        Task<bool> EliminarAsync(int id);
        Task<FacturaCompra?> ObtenerAsync(int id);
        Task<IQueryable<FacturaCompra>> ObtenerTodoAsync();
        Task<IEnumerable<FacturaCompraDetalle>> ObtenerLineasAsync(int id);
        Task<IEnumerable<FacturaCompra>> ObtenerFacturasCompraBaseAsync(string codigoSn);
        Task<FacturaCompraBase> ObtenerFacturaCompraBaseAsync(int entry);
        #endregion
    }
}
