namespace API.Domain.Entity.Models;

/// <summary>
/// Forma de pago de un pago (cobro o pago a proveedor): contrato común para validar las formas de pago
/// de ambos módulos con las mismas reglas.
/// </summary>
public interface IMedioPago
{
    string CodFormaPago { get; set; }
    decimal Monto { get; set; }
    string? Referencia { get; set; }
    string? Banco { get; set; }
    DateTime? FechaCheque { get; set; }
}
