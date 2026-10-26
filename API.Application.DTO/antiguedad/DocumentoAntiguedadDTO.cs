namespace API.Application.DTO.antiguedad
{
    /// <summary>Documento abierto de un socio con sus días de vencido (negativo o cero = aún vigente).</summary>
    public class DocumentoAntiguedadDTO
    {
        public int Entry { get; set; }
        public string Tipo { get; set; } = null!;
        public int NumDoc { get; set; }
        public DateTime FechaDoc { get; set; }
        public DateTime Vencimiento { get; set; }
        public int DiasVencido { get; set; }
        public decimal Saldo { get; set; }
    }
}
