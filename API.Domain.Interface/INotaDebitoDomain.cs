using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface INotaDebitoDomain
    {
        #region async methods
        Task<int> InsertarAsync(Factura obj, IEnumerable<FacturaDetalle> lineas);
        Task<bool> ActualizarAsync(int id, Factura obj);
        Task<bool> EliminarAsync(int id);
        Task<Factura?> ObtenerAsync(int id);
        Task<IQueryable<Factura>> ObtenerTodoAsync();
        Task<IEnumerable<FacturaDetalle>> ObtenerLineasAsync(int id);
        Task<IEnumerable<Factura>> ObtenerFacturasBaseAsync(string codigoSn);
        Task<FacturaBase> ObtenerFacturaBaseAsync(int entry);
        #endregion
    }
}
