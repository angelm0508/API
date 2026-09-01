using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface ISalidaMercanciaDetalleDomain
    {
        #region async methods
        Task<int> InsertarAsync(SalidaMercanciaDetalle obj);
        Task<bool> ActualizarAsync(int entry, int noLinea, SalidaMercanciaDetalle obj);
        Task<bool> EliminarAsync(int entry, int noLinea);
        Task<SalidaMercanciaDetalle> ObtenerAsync(int entry, int noLinea);
        Task<IQueryable<SalidaMercanciaDetalle>> ObtenerTodoAsync();
        Task<IEnumerable<SalidaMercanciaDetalle>> ObtenerPorSalidaMercanciaAsync(int entry);
        #endregion
    }
}
