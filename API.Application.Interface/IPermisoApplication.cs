using API.Application.DTO;
using API.Application.DTO.permiso;

namespace API.Application.Interface
{
    public interface IPermisoApplication
    {
        Task<Respuesta<IEnumerable<ModuloDTO>>> ObtenerModulosAsync();
        Task<Respuesta<IEnumerable<PermisoModuloDTO>>> ObtenerMatrizUsuarioAsync(int usuarioId);
        Task<Respuesta<bool>> GuardarMatrizUsuarioAsync(int usuarioId, List<PermisoModuloDTO> matriz);
        Task<Respuesta<IEnumerable<PermisoModuloDTO>>> ObtenerMatrizPlantillaAsync(int plantillaId);
        Task<Respuesta<bool>> GuardarMatrizPlantillaAsync(int plantillaId, List<PermisoModuloDTO> matriz);
        Task<Respuesta<bool>> AplicarPlantillaAsync(int usuarioId, int plantillaId);
    }
}
