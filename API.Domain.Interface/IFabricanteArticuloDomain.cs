using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IFabricanteArticuloDomain
    {
        #region async methods
        Task<int> InsertarAsync(FabricanteArticulo obj);
        Task<bool> ActualizarAsync(int id, FabricanteArticulo obj);
        Task<bool> EliminarAsync(int id);
        Task<FabricanteArticulo> ObtenerAsync(int id);
        Task<FabricanteArticulo> ObtenerAsync(string name);
        Task<IQueryable<FabricanteArticulo>> ObtenerTodoAsync();
        Task<IEnumerable<FabricanteArticulo>> ObtenerContengaNombreAsync(string name);
        #endregion
    }
}
