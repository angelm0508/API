namespace API.Application.DTO.impuesto
{
    public class ImpuestoDTO
    {
        public string Codigo { get; set; } = null!;

        public string Nombre { get; set; } = null!;

        public decimal? Tasa { get; set; }
    }
}
