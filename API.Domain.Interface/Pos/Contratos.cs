namespace API.Domain.Interface.Pos
{
    public sealed record PosConfiguracionDato(
        int? SerieFactura, int? SeriePago, string? CodAlmacen, string? CodigoImpuesto,
        string? ClienteDefecto, string? FormaPagoEfectivo, string? MonedaDoc);

    public sealed record PosConfiguracionVista(PosConfiguracionDato Configuracion, IReadOnlyList<string> Faltantes);

    /// <summary>Forma de pago activa tal como la necesita la pantalla del POS.</summary>
    public sealed record FormaPagoPos(string Codigo, string Nombre, string TipoDato);

    /// <summary>
    /// Todo lo que la pantalla del POS necesita al cargar, bajo el permiso Pos.Ver (sin pedir permisos de otros módulos):
    /// la configuración con sus faltantes, las formas de pago activas, la tasa del impuesto configurado (0 si no hay) y el
    /// nombre y NIT del cliente por defecto (nulos si no hay o no existe).
    /// </summary>
    public sealed record PosContexto(
        PosConfiguracionVista Configuracion, IReadOnlyList<FormaPagoPos> FormasPago, decimal TasaImpuesto,
        string? NombreCliente, string? NitCliente);

    /// <summary>Cliente para «Cambiar cliente» del POS (la lista de precios se resuelve en el servidor con <c>cliente=</c>).</summary>
    public sealed record ClientePos(string Codigo, string Nombre, string? Nit, int? NumLstPrecio);

    /// <summary>
    /// «Cliente nuevo» rápido del POS (<c>POST api/Pos/Clientes</c>): solo nombre y NIT (CF si es consumidor final). Se crea
    /// de contado (límite de crédito 0) en la serie automática por defecto de clientes.
    /// </summary>
    public sealed record NuevoClientePos(string? Nombre, string? Nit);

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
