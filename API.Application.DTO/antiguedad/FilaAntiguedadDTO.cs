namespace API.Application.DTO.antiguedad
{
    /// <summary>Saldo de un socio de negocio repartido por antigüedad de vencimiento (al día de hoy).</summary>
    public class FilaAntiguedadDTO
    {
        public string CodigoSn { get; set; } = null!;
        public string NombreSn { get; set; } = null!;
        public decimal Vigente { get; set; }
        public decimal De1a30 { get; set; }
        public decimal De31a60 { get; set; }
        public decimal De61a90 { get; set; }
        public decimal Mas90 { get; set; }
        public decimal Total { get; set; }
    }
}
