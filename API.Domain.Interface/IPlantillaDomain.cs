using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IPlantillaDomain
    {
        Task<int> InsertarAsync(Plantilla obj);
        Task<bool> ActualizarAsync(int id, Plantilla obj);
        Task<bool> EliminarAsync(int id);
        Task<Plantilla> ObtenerAsync(int id);
        Task<IQueryable<Plantilla>> ObtenerTodoAsync();
    }
}
