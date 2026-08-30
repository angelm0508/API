using API.Application.DTO;
using API.Application.DTO.articulo.grupo_unidad_medida_det_articulo;

namespace API.Application.Interface
{
    public interface IGrupoUnidadMedidaDetArticuloApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(GrupoUnidadMedidaDetArticuloCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int grpMedidaEntry, int numLinea, GrupoUnidadMedidaDetArticuloActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int grpMedidaEntry, int numLinea);
        Task<Respuesta<GrupoUnidadMedidaDetArticuloDTO>> ObtenerAsync(int grpMedidaEntry, int numLinea);
        Task<Respuesta<IEnumerable<GrupoUnidadMedidaDetArticuloDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<GrupoUnidadMedidaDetArticuloDTO>>> ObtenerPorGrupoAsync(int grpMedidaEntry);
        #endregion
    }
}
