using API.Application.DTO;
using API.Application.DTO.permiso;

namespace API.Application.Interface
{
    public interface IPlantillaApplication
    {
        Task<Respuesta<int>> InsertarAsync(PlantillaCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, PlantillaActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<PlantillaDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<PlantillaDTO>>> ObtenerTodoAsync();
    }
}
