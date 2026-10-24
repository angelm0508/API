namespace API.Domain.Interface
{
    /// <summary>Saldo de una factura o nota de débito de cliente: total − notas de crédito activas − pagos activos.</summary>
    public sealed record SaldoFactura(int FacturaEntry, decimal Total, decimal Acreditado, decimal Pagado, decimal Saldo, string Estado);

    /// <summary>Un pago activo aplicado a un documento (para el mensaje de "cancele primero el pago").</summary>
    public sealed record PagoAplicado(int PagoEntry, int PagoNumDoc, decimal Monto);

    public static class EstadoPagoFactura
    {
        public const string Pendiente = "Pendiente";
        public const string Parcial = "Parcial";
        public const string Pagada = "Pagada";
    }

    public interface ISaldoFacturaService
    {
        /// <summary>
        /// Saldo y estado de pago de las facturas pedidas, con tres consultas agrupadas (no una por factura).
        /// Una factura inexistente no aparece en el diccionario.
        /// </summary>
        Task<IReadOnlyDictionary<int, SaldoFactura>> ObtenerAsync(IEnumerable<int> facturaEntries);

        /// <summary>Pagos no cancelados aplicados a un documento, del más antiguo al más reciente.</summary>
        Task<IReadOnlyList<PagoAplicado>> ObtenerPagosActivosAsync(int facturaEntry);
    }
}
