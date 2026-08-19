namespace API.Application.DTO.autenticacion
{
    public class LoginResponseDTO
    {
        public bool Resultado { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public string? Token { get; set; }
        public string? UsuarioNombre { get; set; }
        public DateTime? ExpirasEn { get; set; }
    }
}
