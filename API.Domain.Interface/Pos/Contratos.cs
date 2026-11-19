namespace API.Domain.Interface.Pos
{
    public sealed record PosConfiguracionDato(
        int? SerieFactura, int? SeriePago, string? CodAlmacen, string? CodigoImpuesto,
        string? ClienteDefecto, string? FormaPagoEfectivo, string? MonedaDoc);

    public sealed record PosConfiguracionVista(PosConfiguracionDato Configuracion, IReadOnlyList<string> Faltantes);

    public sealed record ArticuloPos(
        string Codigo, string? CodigoBarras, string Nombre, decimal Precio, decimal Existencias, bool Inventariable);

    public sealed record VentaPosLinea(string CodArticulo, decimal Cantidad);

    public sealed record VentaPosPago(string CodFormaPago, decimal Monto, string? Referencia, string? Banco, DateTime? FechaCheque);

    public sealed record VentaPosSolicitud(
        string? CodigoCliente, string? Nit, List<VentaPosLinea> Lineas, List<VentaPosPago> Pagos);

    public sealed record TicketPosLinea(string CodArticulo, string Descripcion, decimal Cantidad, decimal PrecioNeto, decimal Total);

    public sealed record TicketPosPago(string CodFormaPago, string Forma, decimal Monto);

    public sealed record TicketPos(
        int FacturaEntry, int Serie, string NombreSerie, int NumDoc, DateTime Fecha, string Cajero,
        string CodigoCliente, string NombreCliente, string Nit,
        IReadOnlyList<TicketPosLinea> Lineas, decimal Subtotal, decimal Impuesto, decimal Total,
        IReadOnlyList<TicketPosPago> Pagos, decimal Pagado, decimal Cambio, string Moneda, string Empresa);

    public sealed record VentaPosDelDia(int FacturaEntry, int Serie, int NumDoc, DateTime Fecha, string CodigoCliente, string NombreCliente, decimal Total, bool Cancelada);
}
