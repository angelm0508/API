using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.articulo.grupo_sn
{
    public class GrupoSnCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string? Nombre { get; set; }

        public string? Bloqueado { get; set; }
    }
}
