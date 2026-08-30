using API.Application.DTO;
using API.Application.DTO.articulo.grupo_unidad_medida_articulo;

namespace API.Application.Interface
{
    public interface IGrupoUnidadMedidaArticuloApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(GrupoUnidadMedidaArticuloCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, GrupoUnidadMedidaArticuloActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<GrupoUnidadMedidaArticuloDTO>> ObtenerAsync(int id);
        Task<Respuesta<GrupoUnidadMedidaArticuloDTO>> ObtenerAsync(string name);
        Task<Respuesta<IEnumerable<GrupoUnidadMedidaArticuloDTO>>> ObtenerContengaNombreAsync(string name);
        Task<Respuesta<IEnumerable<GrupoUnidadMedidaArticuloDTO>>> ObtenerTodoAsync();
        #endregion
    }
}
