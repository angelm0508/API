using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;

namespace API.Domain.Core
{
    public class PlantillaDomain : IPlantillaDomain
    {
        private readonly IRepositorioGenerico<Plantilla, int> _repo;

        public PlantillaDomain(IRepositorioGenerico<Plantilla, int> repo)
        {
            _repo = repo;
        }

        public async Task<int> InsertarAsync(Plantilla obj)
        {
            var creado = await _repo.InsertarAsync(obj);
            return creado.Id;
        }

        public async Task<bool> ActualizarAsync(int id, Plantilla obj) => await _repo.ActualizarAsync(id, obj);
        public async Task<bool> EliminarAsync(int id) => await _repo.EliminarAsync(id);
        public async Task<Plantilla> ObtenerAsync(int id) => await _repo.ObtenerAsync(id);
        public async Task<IQueryable<Plantilla>> ObtenerTodoAsync() => await _repo.ObtenerTodoAsync();
    }
}
