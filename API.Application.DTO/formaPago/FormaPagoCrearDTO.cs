namespace API.Application.DTO.formaPago
{
    public class FormaPagoCrearDTO
    {
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public string? TipoDato { get; set; }
        public string? Activo { get; set; }
    }
}
