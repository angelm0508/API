namespace API.Application.DTO.pagoRecibido
{
    public class PagoRecibidoFacturaDTO
    {
        public int Entry { get; set; }
        public int NoLinea { get; set; }
        public int FacturaEntry { get; set; }
        public int NumDoc { get; set; }
        /// <summary>"--" = factura; "ND" = nota de débito de cliente.</summary>
        public string? DocSubType { get; set; }
        public decimal? TotalDoc { get; set; }
        public decimal MontoAplicado { get; set; }
    }
}
