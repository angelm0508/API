using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.numeracionDocumento
{
    public class NumeracionDocumentoCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string CodigoObj { get; set; } = null!;

        public int? SerieDfct { get; set; }

        public string? DocAlias { get; set; }

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string SubTipoDoc { get; set; } = null!;
    }
}
