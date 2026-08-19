using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.pais
{
    public class PaisCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string Codigo { get; set; } = null!;

        public string? Nombre { get; set; }

        public string? Iso2codigo { get; set; }

        public string? Iso3codigo { get; set; }

        public string? Isonumerico { get; set; }
    }
}
