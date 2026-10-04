using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    /// <summary>Factura de venta candidata a base de una nota de credito de cliente, con lo ya acreditado.</summary>
    public record FacturaAcreditable(Factura Documento, decimal TotalAcreditado);

    /// <summary>Factura de venta con sus lineas y lo ya acreditado: base de una nota de credito de cliente.</summary>
    public record FacturaBaseCredito(Factura Documento, IReadOnlyList<FacturaDetalle> Lineas, decimal TotalAcreditado);

    /// <summary>Factura de compra candidata a base de una nota de credito de proveedor, con lo ya acreditado.</summary>
    public record FacturaCompraAcreditable(FacturaCompra Documento, decimal TotalAcreditado);

    /// <summary>Factura de compra con sus lineas y lo ya acreditado: base de una nota de credito de proveedor.</summary>
    public record FacturaCompraBaseCredito(FacturaCompra Documento, IReadOnlyList<FacturaCompraDetalle> Lineas, decimal TotalAcreditado);
}
