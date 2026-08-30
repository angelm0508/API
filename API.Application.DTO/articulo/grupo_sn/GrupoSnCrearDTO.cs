using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.articulo.grupo_sn
{
    public class GrupoSnCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string? Nombre { get; set; }

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        [StringLength(1)]
        public string TipoGrupo { get; set; } = string.Empty;

        public string? Bloqueado { get; set; }
    }
}
