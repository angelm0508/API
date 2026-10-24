using API.Domain.Interface;

namespace API.Domain.Core
{
    /// <summary>
    /// Regla compartida por factura y nota de débito de cliente: un documento con pagos activos no se cancela ni
    /// se elimina; primero hay que cancelar el pago.
    /// </summary>
    public static class GuardaPagos
    {
        public static async Task ValidarSinPagosAsync(ISaldoFacturaService saldos, int facturaEntry)
        {
            var pagos = await saldos.ObtenerPagosActivosAsync(facturaEntry);
            if (pagos.Count == 0)
                return;

            var etiquetas = string.Join(", ", pagos.Select(p => $"Pago {p.PagoNumDoc}"));
            throw new Exception($"El documento tiene pagos aplicados ({etiquetas}). Cancele primero el pago.");
        }
    }
}
