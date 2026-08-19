using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.numeracionDocumento
{
    public class NumeracionDocumentoDTO
    {
        public string CodigoObj { get; set; } = null!;

        public int? SerieDfct { get; set; }

        public string? DocAlias { get; set; }

        public string SubTipoDoc { get; set; } = null!;
    }
}
