namespace API.Infraestructure.Interface
{
    /// <summary>
    /// Candado de actualización sobre la fila de una serie (<c>NumeracionDocumentoDet</c>) hasta el fin de la transacción
    /// activa. Lo usa la venta POS para poner en fila a las cajas que numeran sobre las mismas series: sin él, bajo
    /// SERIALIZABLE, dos ventas leen la serie con candado compartido y se interbloquean al actualizar <c>SigNumero</c>.
    /// Solo vale dentro de una transacción; fuera de ella lanza <see cref="InvalidOperationException"/>.
    /// </summary>
    public interface IBloqueoSeries
    {
        Task BloquearAsync(int serie);
    }
}
