namespace API.Domain.Entity.Models;

public partial class Factura
{
    /// <summary>
    /// Monto que el usuario va a cobrar en el acto al facturar (no se guarda: la Web registra el pago aparte, en
    /// PagosRecibidos). Solo lo lee el control del límite de crédito para restarlo del monto nuevo de la factura.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal PagoInmediato { get; set; }
}
