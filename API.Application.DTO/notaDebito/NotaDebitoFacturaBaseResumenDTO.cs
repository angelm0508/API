namespace API.Application.DTO.notaDebito
{
    /// <summary>Resumen de una factura candidata a base de una nota de debito.</summary>
    public class NotaDebitoFacturaBaseResumenDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public DateTime? FechaDoc { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? MonedaDoc { get; set; }
    }
}
