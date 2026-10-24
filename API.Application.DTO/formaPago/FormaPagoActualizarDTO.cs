namespace API.Application.DTO.formaPago
{
    public class FormaPagoActualizarDTO
    {
        public string Nombre { get; set; } = null!;
        public string? TipoDato { get; set; }
        public string? Activo { get; set; }
    }
}
