using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.socioNegocio
{
    public class SocioNegocioDTO
    {
        public string Codigo { get; set; } = null!;

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

        public string? CondicionPago { get; set; }

        /// <summary>Límite de crédito del cliente. null = sin límite; 0 = solo contado.</summary>
        public decimal? LimiteCredito { get; set; }

        /// <summary>"S" bloquea documentos nuevos si el cliente tiene saldo vencido; siempre "S" o "N".</summary>
        public string? BloquearSiVencido { get; set; }

        public int Serie { get; set; }
    }
}
