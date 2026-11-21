namespace API.Domain.Interface.Existencias
{
    public sealed record ParExistencia(string CodArticulo, string CodAlmacen);

    public sealed record ResultadoRecalculo(int Filas, int Corregidas);

    /// <summary>Códigos de objeto de los documentos que mueven el comprometido (ventas) y el pedido (compras).</summary>
    public static class TipoDocCompromiso
    {
        public const int Pedido = 4;
        public const int Entrega = 5;
        public const int Factura = 6;
        public const int PedidoCompra = 11;
        public const int EntregaCompra = 12;
        public const int FacturaCompra = 13;
    }
}
