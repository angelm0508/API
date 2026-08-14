using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.articulo.grupo_articulo
{
    public class GrupoArticuloCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string Nombre { get; set; }
    }
}
