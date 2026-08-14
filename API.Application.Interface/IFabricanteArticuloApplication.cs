using API.Application.DTO;
using API.Application.DTO.articulo.fabricante_articulo;

namespace API.Application.Interface
{
    public interface IFabricanteArticuloApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(FabricanteArticuloCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, FabricanteArticuloActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<FabricanteArticuloDTO>> ObtenerAsync(int id);
        Task<Respuesta<FabricanteArticuloDTO>> ObtenerAsync(string name);
        Task<Respuesta<IEnumerable<FabricanteArticuloDTO>>> ObtenerContengaNombreAsync(string name);
        Task<Respuesta<IEnumerable<FabricanteArticuloDTO>>> ObtenerTodoAsync();
        #endregion
    }
}
