using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.municipio
{
    public class MunicipioCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string Codigo { get; set; } = null!;

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string Departamento { get; set; } = null!;

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string Pais { get; set; } = null!;

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string Nombre { get; set; } = null!;
    }
}
