using API.Domain.Interface;

namespace API.Domain.Core
{
    /// <summary>
    /// Regla compartida por factura de compra y nota de débito de proveedor: un documento con pagos activos no se
    /// cancela ni se elimina; primero hay que cancelar el pago.
    /// </summary>
    public static class GuardaPagosEfectuados
    {
        public static async Task ValidarSinPagosAsync(ISaldoFacturaCompraService saldos, int facturaCompraEntry)
        {
            var pagos = await saldos.ObtenerPagosActivosAsync(facturaCompraEntry);
            if (pagos.Count == 0)
                return;

            var etiquetas = string.Join(", ", pagos.Select(p => $"Pago {p.PagoNumDoc}"));
            throw new Exception($"El documento tiene pagos aplicados ({etiquetas}). Cancele primero el pago.");
        }

        /// <summary>Un documento que figura en algún pago (aunque esté cancelado) conserva su historial: no se elimina.</summary>
        public static async Task ValidarSinHistorialAsync(ISaldoFacturaCompraService saldos, int facturaCompraEntry)
        {
            if (await saldos.TienePagosRegistradosAsync(facturaCompraEntry))
                throw new Exception("El documento tiene pagos registrados (cancelados) y no se puede eliminar; consérvelo cancelado.");
        }
    }
}
