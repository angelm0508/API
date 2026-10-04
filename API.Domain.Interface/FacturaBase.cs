using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    /// <summary>Factura de venta con sus lineas: base de una nota de debito de cliente.</summary>
    public record FacturaBase(Factura Documento, IReadOnlyList<FacturaDetalle> Lineas);

    /// <summary>Factura de compra con sus lineas: base de una nota de debito de proveedor.</summary>
    public record FacturaCompraBase(FacturaCompra Documento, IReadOnlyList<FacturaCompraDetalle> Lineas);
}
