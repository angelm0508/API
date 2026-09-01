using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface ISalidaMercanciaDomain
    {
        #region async methods
        Task<int> InsertarAsync(SalidaMercancia obj, IEnumerable<SalidaMercanciaDetalle> lineas);
        Task<bool> ActualizarAsync(int id, SalidaMercancia obj);
        Task<bool> EliminarAsync(int id);
        Task<SalidaMercancia> ObtenerAsync(int id);
        Task<IQueryable<SalidaMercancia>> ObtenerTodoAsync();
        #endregion
    }
}
