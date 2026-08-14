using API.Application.DTO;
using API.Application.DTO.articulo.articulo;

namespace API.Application.Interface
{
    public interface IArticuloApplication
    {
        #region async methods
        Task<Respuesta<bool>> InsertarAsync(ArticuloCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(string sku, ArticuloActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(string sku);
        Task<Respuesta<ArticuloDTO>> ObtenerPorNombreAsync(string name);
        Task<Respuesta<ArticuloDTO>> ObtenerPorCodigoAsync(string sku);
        Task<Respuesta<IEnumerable<ArticuloDTO>>> ObtenerAsync();
        //Task<Respuesta<PagedList<ArticuloDTO>>> GetAllWithPagingAsync(PaginationParametersDTO paginationParametersDTO);
        Task<Respuesta<IEnumerable<ArticuloDTO>>> ObtenerContenganNombreAsync(string name);
        Task<Respuesta<IEnumerable<ArticuloDTO>>> ObtenerContenganCodigoAsync(string sku);
        #endregion
    }
}
