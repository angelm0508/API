using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.moneda
{
    public class MonedaCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string Codigo { get; set; } = null!;

        public string? Nombre { get; set; }

        public string? NombreImpresion { get; set; }

        public string? Centena { get; set; }

        public string? CodigoIso { get; set; }

        public short? TipoReondeo { get; set; }
    }
}
