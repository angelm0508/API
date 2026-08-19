using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.almacen
{
    public class AlmacenDTO
    {
        public string Codigo { get; set; } = null!;

        public string? Nombre { get; set; }

        public string Activo { get; set; } = null!;

        public string? Calle { get; set; }

        public string? CodigoPostal { get; set; }

        public string? Pais { get; set; }

        public string? Municipio { get; set; }

        public string? Departamento { get; set; }

        public string? Bloqueado { get; set; }
    }
}
