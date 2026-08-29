namespace API.Application.DTO.impuesto
{
    public class ImpuestoActualizarDTO
    {
        public string Nombre { get; set; } = null!;

        public decimal? Tasa { get; set; }
    }
}
