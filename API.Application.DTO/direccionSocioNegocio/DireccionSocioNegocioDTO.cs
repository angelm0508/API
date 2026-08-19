using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.direccionSocioNegocio
{
    public class DireccionSocioNegocioDTO
    {
        public string Direccion { get; set; } = null!;

        public string CodigoSn { get; set; } = null!;

        public string? Calle { get; set; }

        public string? Bloque { get; set; }

        public string? CodigoPostal { get; set; }

        public string? Pais { get; set; }

        public string? Municipio { get; set; }

        public string? Departamento { get; set; }

        public int? NumLinea { get; set; }

        public string? TipoDireccion { get; set; }
    }
}
