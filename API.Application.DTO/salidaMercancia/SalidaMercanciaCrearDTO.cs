using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.salidaMercancia
{
    public class SalidaMercanciaCrearDTO
    {
        // Requerido solo cuando la serie elegida es "Manual" -- para series autogeneradas el
        // servidor calcula el siguiente número al momento de registrar la salida de mercancía (ver
        // SalidaMercanciaDomain.InsertarAsync), así que aquí no puede ser obligatorio.
        public int? NumDoc { get; set; }

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public int Serie { get; set; }

        public string? NumManual { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaContab { get; set; }
        public string? Referencia { get; set; }
        public string? Comentario { get; set; }

        // La salida de mercancía es un ajuste de inventario sin socio de negocio: el cliente no
        // decide el estado de cancelación. Se acepta el campo para no romper formularios existentes
        // pero el servidor lo ignora y fuerza Cancelado='N' al registrar.
        public string? Cancelado { get; set; }

        /// <summary>
        /// Líneas del documento. El documento se registra con sus líneas en una sola petición
        /// (y una sola transacción). El `Entry` de cada línea se ignora aquí (lo asigna el
        /// servidor al crear el encabezado).
        /// </summary>
        public List<SalidaMercanciaDetalleCrearDTO> Lineas { get; set; } = new();
    }
}
