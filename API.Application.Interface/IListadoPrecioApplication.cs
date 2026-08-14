using API.Application.DTO;
using API.Application.DTO.precio.listado_precio;

namespace API.Application.Interface
{
    public interface IListadoPrecioApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(ListadoPrecioCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, ListadoPrecioActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<ListadoPrecioDTO>> ObtenerAsync(int id);
        Task<Respuesta<ListadoPrecioDTO>> ObtenerAsync(string name);
        Task<Respuesta<IEnumerable<ListadoPrecioDTO>>> ObtenerContengaNombreAsync(string name);
        Task<Respuesta<IEnumerable<ListadoPrecioDTO>>> ObtenerTodoAsync();
        #endregion
    }
}
