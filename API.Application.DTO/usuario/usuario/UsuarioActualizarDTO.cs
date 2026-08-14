namespace API.Application.DTO.usuario.usuario
{
    public class UsuarioActualizarDTO
    {
        public string Codigo { get; set; }
        public string? Password { get; set; }
        public string? Nombre { get; set; }
        public string? Email { get; set; }
        public string? SuperUsuario { get; set; }
        public string? Bloqueado { get; set; }
    }
}
