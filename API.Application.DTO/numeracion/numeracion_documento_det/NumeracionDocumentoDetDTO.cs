namespace API.Application.DTO.numeracion.numeracion_documento_det
{
    public class NumeracionDocumentoDetDTO
    {
        public string CodigoObj { get; set; }
        public int Serie { get; set; }
        public string NombreSerie { get; set; }
        public int? IniNumero { get; set; }
        public int? SigNumero { get; set; }
        public int? FinNumero { get; set; }
        public string? IniCadena { get; set; }
        public string? FinCadena { get; set; }
        public string? Comentario { get; set; }
        public string? Bloqueado { get; set; }
        public int? CantDigitos { get; set; }
        public string SubTipoDoc { get; set; }
    }
}
