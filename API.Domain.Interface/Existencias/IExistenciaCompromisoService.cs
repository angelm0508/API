namespace API.Domain.Interface.Existencias
{
    public interface IExistenciaCompromisoService
    {
        /// <summary>Recalcula Comprometido y Pedido de esos (artículo, almacén). Idempotente; deja en 0 los que no tienen pedidos abiertos.</summary>
        Task RecalcularAsync(IReadOnlyCollection<ParExistencia> pares);

        /// <summary>Recalcula los pares que toca un documento (ver ObtenerParesAsync). Puede lanzar.</summary>
        Task RecalcularPorDocumentoAsync(int tipoDoc, int entry);

        /// <summary>Recalcula todos los pares con pedidos abiertos y deja en 0 los demás. Devuelve filas revisadas y filas cambiadas.</summary>
        Task<ResultadoRecalculo> RecalcularTodoAsync();

        /// <summary>
        /// Pares (artículo, almacén) que toca un documento: pedido (4, 11) = los de sus líneas; entrega/factura (5, 6, 12, 13) =
        /// los de sus propias líneas más los de las líneas de los pedidos base (BaseTipo 4 en ventas, 11 en compras).
        /// Úsalo ANTES de borrar un documento.
        /// </summary>
        Task<IReadOnlyList<ParExistencia>> ObtenerParesAsync(int tipoDoc, int entry);

        /// <summary>Envoltorio seguro de RecalcularPorDocumentoAsync: NUNCA lanza (atrapa y registra).</summary>
        Task NotificarDocumentoAsync(int tipoDoc, int entry);

        /// <summary>Envoltorio seguro de RecalcularAsync: NUNCA lanza (atrapa y registra).</summary>
        Task NotificarParesAsync(IReadOnlyCollection<ParExistencia> pares);
    }
}
