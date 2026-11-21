using API.Application.DTO;
using API.Application.DTO.inventario;
using API.Domain.Interface.Existencias;

namespace API.Application.Interface
{
    public interface IExistenciaApplication
    {
        Task<Respuesta<IEnumerable<ExistenciaArticuloDTO>>> ObtenerTodoAsync(string? articulo, string? almacen);
        Task<Respuesta<ExistenciaArticuloDTO>> ObtenerAsync(string codArticulo, string codAlmacen);
        Task<Respuesta<IEnumerable<ExistenciaArticuloDTO>>> ObtenerPorArticuloAsync(string codArticulo);
        Task<Respuesta<ResultadoRecalculo>> RecalcularCompromisosAsync();
    }
}
