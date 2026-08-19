using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.pais
{
    public class PaisActualizarDTO
    {
        public string? Nombre { get; set; }

        public string? Iso2codigo { get; set; }

        public string? Iso3codigo { get; set; }

        public string? Isonumerico { get; set; }
    }
}
