using API.Application.DTO;
using API.Application.DTO.articulo.almacen;

namespace API.Application.Interface
{
    public interface IArticuloAlmacenApplication
    {
        #region async methods
        /// <summary>Filas de la ficha de stock por almacén. Resultado falso con «El artículo X no existe.» si no existe.</summary>
        Task<Respuesta<IEnumerable<ArticuloAlmacenDTO>>> ObtenerAsync(string codigo);

        /// <summary>Reemplaza toda la configuración del artículo; nulo o vacío = borra toda la configuración.</summary>
        Task<Respuesta<bool>> GuardarAsync(string codigo, IEnumerable<ArticuloAlmacenGuardarDTO>? filas);

        /// <summary>Artículos bajo su mínimo por almacén.</summary>
        Task<Respuesta<IEnumerable<AlmacenBajoMinimoDTO>>> ObtenerBajoMinimoAsync();
        #endregion
    }
}
