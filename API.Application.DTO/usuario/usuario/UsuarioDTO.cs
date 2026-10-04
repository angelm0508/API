namespace API.Application.DTO.usuario.usuario
{
    /// <summary>
    /// Representación pública de un usuario. Nunca expone el hash de la contraseña ni UltimaContra.
    /// </summary>
    public class UsuarioDTO
    {
        public int Id { get; set; }
        public int LlaveInterna { get; set; }
        public string Codigo { get; set; }
        public string? Nombre { get; set; }
        public string? Eliminado { get; set; }
        public string? SuperUsuario { get; set; }
        public string? Email { get; set; }
        public string? Bloqueado { get; set; }
    }
}
