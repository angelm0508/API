using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.socioNegocio
{
    public class SocioNegocioActualizarDTO
    {
        public string? Nombre { get; set; }

        public string? TipoSn { get; set; }

        public short? GrupoSn { get; set; }

        public string? Cui { get; set; }

        public string? Nit { get; set; }

        public string? PersContacto { get; set; }

        public string? Tel1 { get; set; }

        public string? Tel2 { get; set; }

        public decimal? Descuento { get; set; }

        public int? NumLstPrecio { get; set; }

        public string? Email { get; set; }

        public string? Activo { get; set; }

        /// <summary>Condición de pago por defecto del socio (código). Vacío la quita (CON al emitir documentos).</summary>
        public string? CondicionPago { get; set; }

        /// <summary>Límite de crédito (solo clientes). null lo quita (sin límite); 0 = solo contado.</summary>
        public decimal? LimiteCredito { get; set; }

        /// <summary>"S" o "N". Vacío = "N".</summary>
        public string? BloquearSiVencido { get; set; }

        public int Serie { get; set; }
    }
}
