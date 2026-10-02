using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;

namespace API.Domain.Core
{
    public class ModuloDomain : IModuloDomain
    {
        private readonly IRepositorioGenerico<Modulo, string> _repo;

        public ModuloDomain(IRepositorioGenerico<Modulo, string> repo)
        {
            _repo = repo;
        }

        public async Task<IQueryable<Modulo>> ObtenerTodoAsync()
        {
            return await _repo.ObtenerTodoAsync();
        }
    }
}
