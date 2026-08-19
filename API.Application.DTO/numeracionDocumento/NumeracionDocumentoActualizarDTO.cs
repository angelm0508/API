using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.numeracionDocumento
{
    public class NumeracionDocumentoActualizarDTO
    {
        public int? SerieDfct { get; set; }

        public string? DocAlias { get; set; }

        public string? SubTipoDoc { get; set; }
    }
}
