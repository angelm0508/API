namespace API.Application.DTO.notaCredito
{
    /// <summary>Resumen de una factura candidata a base de una nota de credito, con su saldo acreditable.</summary>
    public class NotaCreditoFacturaBaseResumenDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public DateTime? FechaDoc { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? MonedaDoc { get; set; }

        /// <summary>"--" = factura; "ND" (cliente) = nota de débito usada como base.</summary>
        public string? DocSubType { get; set; }

        /// <summary>Suma de las notas de credito no canceladas ya emitidas sobre esta factura.</summary>
        public decimal TotalAcreditado { get; set; }

        /// <summary>max(0, TotalDoc - TotalAcreditado): lo que todavia se puede acreditar.</summary>
        public decimal Disponible { get; set; }
    }
}
