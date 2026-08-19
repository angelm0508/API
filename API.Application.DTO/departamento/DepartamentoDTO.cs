using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.departamento
{
    public class DepartamentoDTO
    {
        public string Codigo { get; set; } = null!;

        public string Pais { get; set; } = null!;

        public string? Nombre { get; set; }
    }
}
