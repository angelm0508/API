using API.Application.DTO;
using API.Application.DTO.articulo.unidad_medida_articulo;

namespace API.Application.Interface
{
    public interface IUnidadMedidaArticuloApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(UnidadMedidaArticuloCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, UnidadMedidaArticuloActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<UnidadMedidaArticuloDTO>> ObtenerAsync(int id);
        Task<Respuesta<UnidadMedidaArticuloDTO>> ObtenerAsync(string codigo);
        Task<Respuesta<IEnumerable<UnidadMedidaArticuloDTO>>> ObtenerContengaNombreAsync(string name);
        Task<Respuesta<IEnumerable<UnidadMedidaArticuloDTO>>> ObtenerTodoAsync();
        #endregion
    }
}
