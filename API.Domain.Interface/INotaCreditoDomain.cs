using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface INotaCreditoDomain
    {
        #region async methods
        Task<int> InsertarAsync(NotaCredito obj, IEnumerable<NotaCreditoDetalle> lineas);
        Task<bool> ActualizarAsync(int id, NotaCredito obj);
        Task<bool> EliminarAsync(int id);
        Task<NotaCredito> ObtenerAsync(int id);
        Task<IQueryable<NotaCredito>> ObtenerTodoAsync();
        Task<IEnumerable<NotaCreditoDetalle>> ObtenerLineasAsync(int id);
        Task<IEnumerable<FacturaAcreditable>> ObtenerFacturasBaseAsync(string codigoSn);
        Task<FacturaBaseCredito> ObtenerFacturaBaseAsync(int entry);
        #endregion
    }
}
