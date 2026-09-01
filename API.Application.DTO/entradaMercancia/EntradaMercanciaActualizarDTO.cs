using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.entradaMercancia
{
    public class EntradaMercanciaActualizarDTO
    {
        public int? NumDoc { get; set; }

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public int Serie { get; set; }

        public string? NumManual { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaContab { get; set; }
        public string? Referencia { get; set; }
        public string? Comentario { get; set; }

        // El botón "Cancelar documento" del Web manda { Cancelado: 'S' }; dispara el reverso.
        public string? Cancelado { get; set; }
    }
}
