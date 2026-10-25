namespace API.Application.DTO.pagoEfectuado
{
    public class PagoEfectuadoDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public int Serie { get; set; }
        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public decimal TotalPago { get; set; }
        public string? Comentario { get; set; }
    }
}
