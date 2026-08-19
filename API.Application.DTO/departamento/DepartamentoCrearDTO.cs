using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.departamento
{
    public class DepartamentoCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string Codigo { get; set; } = null!;

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string Pais { get; set; } = null!;

        public string? Nombre { get; set; }
    }
}
