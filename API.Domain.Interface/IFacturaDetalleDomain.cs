using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IFacturaDetalleDomain
    {
        #region async methods
        Task<int> InsertarAsync(FacturaDetalle obj);
        Task<bool> ActualizarAsync(int entry, int noLinea, FacturaDetalle obj);
        Task<bool> EliminarAsync(int entry, int noLinea);
        Task<FacturaDetalle> ObtenerAsync(int entry, int noLinea);
        Task<IQueryable<FacturaDetalle>> ObtenerTodoAsync();
        Task<IEnumerable<FacturaDetalle>> ObtenerPorFacturaAsync(int entry);
        #endregion
    }
}
