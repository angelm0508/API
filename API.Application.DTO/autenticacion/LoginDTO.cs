using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.autenticacion
{
    public class LoginDTO
    {
        [Required(ErrorMessage = "El usuario es requerido.")]
        public string Usuario { get; set; } = null!;

        [Required(ErrorMessage = "La contraseña es requerida.")]
        public string Contrasena { get; set; } = null!;
    }
}
