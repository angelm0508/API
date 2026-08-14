using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IGrupoSnDomain
    {
        #region async methods
        Task<int> InsertarAsync(GrupoSn obj);
        Task<bool> ActualizarAsync(int id, GrupoSn obj);
        Task<bool> EliminarAsync(int id);
        Task<GrupoSn> ObtenerAsync(int id);
        Task<GrupoSn> ObtenerAsync(string name);
        Task<IQueryable<GrupoSn>> ObtenerTodoAsync();
        Task<IEnumerable<GrupoSn>> ObtenerContengaNombreAsync(string name);
        #endregion
    }
}
