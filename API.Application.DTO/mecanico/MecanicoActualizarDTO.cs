namespace API.Application.DTO.mecanico
{
    public class MecanicoActualizarDTO
    {
        public string Nombre { get; set; } = null!;
        public string? Telefono { get; set; }
        public string? Activo { get; set; }
    }
}
