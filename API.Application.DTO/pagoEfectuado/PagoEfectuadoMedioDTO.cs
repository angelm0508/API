namespace API.Application.DTO.pagoEfectuado
{
    public class PagoEfectuadoMedioDTO
    {
        public int Entry { get; set; }
        public int NoLinea { get; set; }
        public string CodFormaPago { get; set; } = null!;
        public string? NombreFormaPago { get; set; }
        public string? TipoDato { get; set; }
        public decimal Monto { get; set; }
        public string? Referencia { get; set; }
        public string? Banco { get; set; }
        public DateTime? FechaCheque { get; set; }
    }
}
