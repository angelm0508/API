using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.articulo.fabricante_articulo
{
    public class FabricanteArticuloCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string Nombre { get; set; }
    }
}
