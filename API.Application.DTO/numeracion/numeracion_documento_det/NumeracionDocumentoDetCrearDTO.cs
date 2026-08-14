using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.numeracion.numeracion_documento_det
{
    public class NumeracionDocumentoDetCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string CodigoObj { get; set; }

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public int Serie { get; set; }

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string NombreSerie { get; set; }

        public int? IniNumero { get; set; }
        public int? SigNumero { get; set; }
        public int? FinNumero { get; set; }
        public string? IniCadena { get; set; }
        public string? FinCadena { get; set; }
        public string? Comentario { get; set; }
        public string? Bloqueado { get; set; }
        public int? CantDigitos { get; set; }

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string SubTipoDoc { get; set; }
    }
}
