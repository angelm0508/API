using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IEntradaMercanciaDomain
    {
        #region async methods
        Task<int> InsertarAsync(EntradaMercancia obj, IEnumerable<EntradaMercanciaDetalle> lineas);
        Task<bool> ActualizarAsync(int id, EntradaMercancia obj);
        Task<bool> EliminarAsync(int id);
        Task<EntradaMercancia> ObtenerAsync(int id);
        Task<IQueryable<EntradaMercancia>> ObtenerTodoAsync();
        #endregion
    }
}
