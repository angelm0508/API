using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.articulo.grupo_unidad_medida_articulo
{
    public class GrupoUnidadMedidaArticuloCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string? Codigo { get; set; }

        public string? Nombre { get; set; }

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public int BaseMedida { get; set; }

        public string? Bloqueado { get; set; }
    }
}
