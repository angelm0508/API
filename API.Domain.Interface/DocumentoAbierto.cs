namespace API.Domain.Interface
{
    /// <summary>
    /// Una línea de un documento origen (Pedido de Compra o Entrega de Compra) con cantidad
    /// pendiente de copiar a un documento destino ("Copiar desde..."). CantidadAbierta se calcula
    /// siempre en el momento -- nunca se persiste -- restando lo ya consumido por líneas activas
    /// (encabezado no cancelado) de los documentos destino posibles.
    /// </summary>
    public record LineaAbierta(
        int NoLinea,
        string CodArticulo,
        string? Descripcion,
        decimal Cantidad,
        decimal CantidadAbierta,
        decimal? Precio,
        string? CodAlmacen,
        string? CodigoImpuesto);

    /// <summary>Un documento origen con al menos una línea de cantidad abierta > 0.</summary>
    public record DocumentoAbierto(
        int Entry,
        int NumDoc,
        int Serie,
        DateTime? FechaDoc,
        IReadOnlyList<LineaAbierta> Lineas);
}
