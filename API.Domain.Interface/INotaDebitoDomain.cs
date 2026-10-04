using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface INotaDebitoDomain
    {
        #region async methods
        Task<int> InsertarAsync(NotaDebito obj, IEnumerable<NotaDebitoDetalle> lineas);
        Task<bool> ActualizarAsync(int id, NotaDebito obj);
        Task<bool> EliminarAsync(int id);
        Task<NotaDebito> ObtenerAsync(int id);
        Task<IQueryable<NotaDebito>> ObtenerTodoAsync();
        Task<IEnumerable<NotaDebitoDetalle>> ObtenerLineasAsync(int id);
        Task<IEnumerable<Factura>> ObtenerFacturasBaseAsync(string codigoSn);
        Task<FacturaBase> ObtenerFacturaBaseAsync(int entry);
        #endregion
    }
}
