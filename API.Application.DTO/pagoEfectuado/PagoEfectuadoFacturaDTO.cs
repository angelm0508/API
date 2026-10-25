namespace API.Application.DTO.pagoEfectuado
{
    public class PagoEfectuadoFacturaDTO
    {
        public int Entry { get; set; }
        public int NoLinea { get; set; }
        public int FacturaCompraEntry { get; set; }
        public int NumDoc { get; set; }
        /// <summary>"--" = factura de compra; "ND" = nota de débito de proveedor.</summary>
        public string? DocSubType { get; set; }
        public decimal? TotalDoc { get; set; }
        public decimal MontoAplicado { get; set; }
    }
}
