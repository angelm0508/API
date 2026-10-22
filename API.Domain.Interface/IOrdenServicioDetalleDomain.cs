using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IOrdenServicioDetalleDomain
    {
        #region async methods
        /// <summary>Agrega una línea a una orden existente (obj.Entry) y devuelve su NoLinea. Los insumos descuentan inventario.</summary>
        Task<int> InsertarAsync(OrdenServicioDetalle obj);
        /// <summary>Cambia una línea (revierte su inventario y vuelve a asentarla). Devuelve false si la línea no existe.</summary>
        Task<bool> ActualizarAsync(int entry, int noLinea, OrdenServicioDetalle obj);
        /// <summary>Quita una línea y devuelve su inventario. Devuelve false si la línea no existe.</summary>
        Task<bool> EliminarAsync(int entry, int noLinea);
        #endregion
    }
}
