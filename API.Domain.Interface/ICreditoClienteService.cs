namespace API.Domain.Interface
{
    /// <summary>Posición de crédito de un cliente: saldo y vencido de sus facturas, valor comprometido en pedidos y entregas abiertos, y disponible frente al límite.</summary>
    public sealed record PosicionCredito(string CodigoSn, decimal? Limite, bool BloquearSiVencido,
        decimal Saldo, decimal Vencido, decimal Comprometido, decimal Exposicion, decimal? Disponible);

    /// <summary>
    /// Resultado de verificar un documento nuevo contra el crédito del cliente. Motivo: "LIMITE", "VENCIDO" o null (permitido).
    /// Posicion es null cuando no se consultó o no aplica (monto cero, socio inexistente o que no es cliente).
    /// </summary>
    public sealed record ResultadoCredito(bool Permitido, string? Motivo /* "LIMITE" | "VENCIDO" | null */,
        PosicionCredito? Posicion, decimal Monto, decimal Exceso);

    public interface ICreditoClienteService
    {
        /// <summary>Posición de crédito del cliente. Socio inexistente: devuelve null.</summary>
        Task<PosicionCredito?> ObtenerPosicionAsync(string codigoSn);
        /// <summary>Posiciones de varios clientes con carga agrupada; los códigos que no son cliente no aparecen.</summary>
        Task<IReadOnlyDictionary<string, PosicionCredito>> ObtenerPosicionesAsync(IEnumerable<string> codigosSn);
        /// <summary>Valor abierto (comprometido) de un pedido o entrega, ignorando si el propio documento está cancelado.
        /// tipo = 4 (Pedido) o 5 (Entrega). Documento inexistente: 0.</summary>
        Task<decimal> ValorAbiertoDocumentoAsync(int tipo, int entry);
        /// <summary>
        /// Monto nuevo que el documento suma a la exposición del cliente: max(0, round2(totalDoc − pagoInmediato − liberado)),
        /// con liberado = Σ de las líneas con base pedido (4) o entrega (5) de Cantidad × valor unitario neto de la línea origen
        /// (el mismo valor que se contó como comprometido). Las líneas con otra base no liberan. Sin líneas base no consulta nada.
        /// </summary>
        Task<decimal> CalcularMontoAsync(int tipoDestino, decimal totalDoc, decimal pagoInmediato, IReadOnlyList<LineaBaseRequest> lineasBase);
        /// <summary>
        /// Verifica un documento nuevo de <paramref name="monto"/> contra el cliente. Orden: monto cero → permitido (sin consultar
        /// saldos); socio inexistente o que no es cliente → permitido (Posicion null); BloquearSiVencido con vencido → VENCIDO
        /// (Exceso = monto); exposición + monto &gt; límite → LIMITE (Exceso = exposición + monto − límite); otro caso permitido.
        /// </summary>
        Task<ResultadoCredito> VerificarAsync(string? codigoSn, decimal monto);
    }
}
