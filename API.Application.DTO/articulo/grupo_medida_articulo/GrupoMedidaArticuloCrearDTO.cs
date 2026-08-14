using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.articulo.grupo_medida_articulo
{
    public class GrupoMedidaArticuloCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string? Nombre { get; set; }

        public string? Bloqueado { get; set; }
    }
}
