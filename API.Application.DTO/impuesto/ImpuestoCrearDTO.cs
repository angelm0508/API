using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.impuesto
{
    public class ImpuestoCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string Codigo { get; set; } = null!;

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string Nombre { get; set; } = null!;

        public decimal? Tasa { get; set; }
    }
}
