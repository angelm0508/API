using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.permiso
{
    public class PlantillaActualizarDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string Nombre { get; set; } = null!;
    }
}
