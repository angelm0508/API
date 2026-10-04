namespace API.Application.DTO.notaCreditoCompra
{
    /// <summary>Resumen de una factura de compra candidata a base de una nota de credito, con su saldo acreditable.</summary>
    public class NotaCreditoCompraFacturaBaseResumenDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public DateTime? FechaDoc { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? MonedaDoc { get; set; }

        /// <summary>Suma de las notas de credito no canceladas ya emitidas sobre esta factura de compra.</summary>
        public decimal TotalAcreditado { get; set; }

        /// <summary>max(0, TotalDoc - TotalAcreditado): lo que todavia se puede acreditar.</summary>
        public decimal Disponible { get; set; }
    }
}
