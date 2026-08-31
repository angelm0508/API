namespace API.Domain.Interface
{
    /// <summary>
    /// Un movimiento de inventario solicitado por un documento. Cantidad &gt; 0 = entrada, &lt; 0 = salida.
    /// </summary>
    public record MovimientoRequest(
        string TipoDoc, int DocEntry, int DocLinea,
        string CodArticulo, string CodAlmacen,
        decimal Cantidad, decimal PrecioUnitario, DateTime Fecha);

    /// <summary>
    /// Aplica movimientos de inventario (existencias, valuación, kardex) sobre el ChangeTracker
    /// del ApiDbTestContext scoped. NUNCA llama SaveChangesAsync: el caller persiste todo junto
    /// con su documento, en una sola transacción implícita (mismo patrón que la numeración).
    /// </summary>
    public interface IInventarioAsientoService
    {
        Task AsentarAsync(IEnumerable<MovimientoRequest> movimientos, bool permitirNegativo = false);

        Task RevertirAsync(string tipoDoc, int docEntry);
    }
}
