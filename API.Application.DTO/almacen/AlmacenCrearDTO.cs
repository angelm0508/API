using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.almacen
{
    public class AlmacenCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string Codigo { get; set; } = null!;

        public string? Nombre { get; set; }

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string Activo { get; set; } = null!;

        public string? Calle { get; set; }

        public string? CodigoPostal { get; set; }

        public string? Pais { get; set; }

        public string? Municipio { get; set; }

        public string? Departamento { get; set; }

        public string? Bloqueado { get; set; }
    }
}
