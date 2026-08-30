using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface ICotizacionDetalleDomain
    {
        #region async methods
        Task<int> InsertarAsync(CotizacionDetalle obj);
        Task<bool> ActualizarAsync(int entry, int noLinea, CotizacionDetalle obj);
        Task<bool> EliminarAsync(int entry, int noLinea);
        Task<CotizacionDetalle> ObtenerAsync(int entry, int noLinea);
        Task<IQueryable<CotizacionDetalle>> ObtenerTodoAsync();
        Task<IEnumerable<CotizacionDetalle>> ObtenerPorCotizacionAsync(int entry);
        #endregion
    }
}
