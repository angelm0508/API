using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.precio.listado_precio
{
    public class ListadoPrecioCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string? Nombre { get; set; }

        public string? Bloqueado { get; set; }
    }
}
