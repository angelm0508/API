namespace API.Application.DTO.creditoCliente
{
    /// <summary>Posición de crédito de un cliente: límite, saldo, vencido, valor comprometido en pedidos y entregas abiertos, y disponible.</summary>
    public class PosicionCreditoDTO
    {
        public string CodigoSn { get; set; } = null!;
        /// <summary>Nulo = sin límite; 0 = solo contado.</summary>
        public decimal? Limite { get; set; }
        public bool BloquearSiVencido { get; set; }
        public decimal Saldo { get; set; }
        public decimal Vencido { get; set; }
        public decimal Comprometido { get; set; }
        /// <summary>Saldo + Comprometido.</summary>
        public decimal Exposicion { get; set; }
        /// <summary>Límite - Exposición (puede ser negativo); nulo sin límite.</summary>
        public decimal? Disponible { get; set; }
        /// <summary>Exposición / Límite × 100 con 1 decimal; nulo si no hay límite o el límite es 0.</summary>
        public decimal? PorcentajeUso { get; set; }
    }
}
