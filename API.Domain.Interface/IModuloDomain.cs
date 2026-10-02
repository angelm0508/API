using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IModuloDomain
    {
        Task<IQueryable<Modulo>> ObtenerTodoAsync();
    }
}
