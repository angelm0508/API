using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.precio.listado_precio
{
    public class ListadoPrecioCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string? Nombre { get; set; }

        public int? Base { get; set; }
        public decimal? Factor { get; set; }
        public short? MetodoRedondeo { get; set; }
        public string? ReglaRedondeo { get; set; }
    }
}
