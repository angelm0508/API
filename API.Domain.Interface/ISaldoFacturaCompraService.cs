namespace API.Domain.Interface
{
    /// <summary>Saldo de una factura de compra o nota de débito de proveedor: total − notas de crédito activas − pagos activos.</summary>
    public sealed record SaldoFacturaCompra(int FacturaCompraEntry, decimal Total, decimal Acreditado, decimal Pagado, decimal Saldo, string Estado);

    public interface ISaldoFacturaCompraService
    {
        /// <summary>
        /// Saldo y estado de pago de las facturas de compra pedidas, con tres consultas agrupadas (no una por factura).
        /// Una factura inexistente no aparece en el diccionario.
        /// </summary>
        Task<IReadOnlyDictionary<int, SaldoFacturaCompra>> ObtenerAsync(IEnumerable<int> facturaCompraEntries);

        /// <summary>Pagos no cancelados aplicados a un documento, del más antiguo al más reciente.</summary>
        Task<IReadOnlyList<PagoAplicado>> ObtenerPagosActivosAsync(int facturaCompraEntry);

        /// <summary>True si el documento figura en algún pago, cancelado o no (historial que impide borrarlo).</summary>
        Task<bool> TienePagosRegistradosAsync(int facturaCompraEntry);
    }
}
