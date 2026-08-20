using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface ICotizacionDomain
    {
        #region async methods
        Task<int> InsertarAsync(Cotizacion obj);
        Task<bool> ActualizarAsync(int id, Cotizacion obj);
        Task<bool> EliminarAsync(int id);
        Task<Cotizacion> ObtenerAsync(int id);
        Task<IQueryable<Cotizacion>> ObtenerTodoAsync();
        #endregion
    }
}
