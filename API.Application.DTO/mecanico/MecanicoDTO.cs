namespace API.Application.DTO.mecanico
{
    public class MecanicoDTO
    {
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public string? Telefono { get; set; }
        public string Activo { get; set; } = null!;
    }
}
