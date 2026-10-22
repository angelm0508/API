using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    /// <summary>
    /// Reglas comunes de las líneas de una orden de servicio: validación, montos calculados en el servidor y
    /// salida de inventario de los insumos. Lo usan OrdenServicioDomain (alta con líneas) y
    /// OrdenServicioDetalleDomain (alta/edición/baja de una línea).
    /// </summary>
    public interface IOrdenServicioLineaService
    {
        /// <summary>
        /// Valida las líneas y completa lo que fija el servidor (descripción, almacén por defecto, EsInsumo,
        /// TipoObjeto, Impuesto y TotalLinea). numeroInicial = número de línea de la primera, solo para los
        /// mensajes "Línea n". Lanza Exception con mensaje de usuario.
        /// </summary>
        Task PrepararAsync(IList<OrdenServicioDetalle> lineas, int numeroInicial = 1);

        /// <summary>
        /// Asienta la salida de inventario de los insumos con cantidad &gt; 0 (TipoDoc "70", bloqueo duro de stock
        /// negativo). Los servicios no mueven inventario. Las líneas ya deben tener Entry y NoLinea.
        /// </summary>
        Task AsentarSalidaAsync(int entry, DateTime fecha, IEnumerable<OrdenServicioDetalle> lineas);

        /// <summary>Recalcula TotalBruto, TotalDesc, TotalImp y TotalDoc de la cabecera a partir de las líneas.</summary>
        void RecalcularCabecera(OrdenServicio orden, IEnumerable<OrdenServicioDetalle> lineas);
    }
}
