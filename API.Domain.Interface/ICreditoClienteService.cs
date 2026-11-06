namespace API.Domain.Interface
{
    /// <summary>Posición de crédito de un cliente: saldo y vencido de sus facturas, valor comprometido en pedidos y entregas abiertos, y disponible frente al límite.</summary>
    public sealed record PosicionCredito(string CodigoSn, decimal? Limite, bool BloquearSiVencido,
        decimal Saldo, decimal Vencido, decimal Comprometido, decimal Exposicion, decimal? Disponible);

    public interface ICreditoClienteService
    {
        /// <summary>Posición de crédito del cliente. Socio inexistente: devuelve null.</summary>
        Task<PosicionCredito?> ObtenerPosicionAsync(string codigoSn);
        /// <summary>Posiciones de varios clientes con carga agrupada; los códigos que no son cliente no aparecen.</summary>
        Task<IReadOnlyDictionary<string, PosicionCredito>> ObtenerPosicionesAsync(IEnumerable<string> codigosSn);
        /// <summary>Valor abierto (comprometido) de un pedido o entrega, ignorando si el propio documento está cancelado.
        /// tipo = 4 (Pedido) o 5 (Entrega). Documento inexistente: 0.</summary>
        Task<decimal> ValorAbiertoDocumentoAsync(int tipo, int entry);
        // VerificarAsync y CalcularMontoAsync se agregan en la Task 4.
    }
}
