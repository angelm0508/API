using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.municipio
{
    public class MunicipioDTO
    {
        public string Codigo { get; set; } = null!;

        public string Departamento { get; set; } = null!;

        public string Pais { get; set; } = null!;

        public string Nombre { get; set; } = null!;
    }
}
