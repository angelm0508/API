using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IFacturaCompraDetalleDomain
    {
        #region async methods
        Task<int> InsertarAsync(FacturaCompraDetalle obj);
        Task<bool> ActualizarAsync(int entry, int noLinea, FacturaCompraDetalle obj);
        Task<bool> EliminarAsync(int entry, int noLinea);
        Task<FacturaCompraDetalle> ObtenerAsync(int entry, int noLinea);
        Task<IQueryable<FacturaCompraDetalle>> ObtenerTodoAsync();
        Task<IEnumerable<FacturaCompraDetalle>> ObtenerPorFacturaCompraAsync(int entry);
        #endregion
    }
}
