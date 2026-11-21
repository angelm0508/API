namespace API.Domain.Interface
{
    /// <summary>Tipos de objeto de la cadena de compras (coinciden con NumeracionDocumento.CodigoObj / TipoObjeto y con el BaseTipo de las líneas).</summary>
    public static class TipoDocumentoCompra
    {
        public const int PedidoCompra = 11;
        public const int EntregaCompra = 12;
        public const int FacturaCompra = 13;

        public static string Nombre(int tipo) => tipo switch
        {
            PedidoCompra => "Pedido de compra",
            EntregaCompra => "Entrega de compra",
            FacturaCompra => "Factura de compra",
            _ => $"Documento {tipo}"
        };

        public static IReadOnlyList<int> DestinosDe(int origen) => origen switch
        {
            PedidoCompra => new[] { EntregaCompra, FacturaCompra },
            EntregaCompra => new[] { FacturaCompra },
            _ => Array.Empty<int>()
        };

        public static bool EsCombinacionValida(int origen, int destino) => DestinosDe(origen).Contains(destino);
    }

    /// <summary>Nombre de un documento de ventas o de compras (los códigos no se solapan: ventas 3, 4, 5, 6, 70; compras 11, 12, 13).</summary>
    public static class NombreDocumento
    {
        public static string De(int tipo) =>
            tipo is TipoDocumentoCompra.PedidoCompra or TipoDocumentoCompra.EntregaCompra or TipoDocumentoCompra.FacturaCompra
                ? TipoDocumentoCompra.Nombre(tipo)
                : TipoDocumentoVenta.Nombre(tipo);
    }

    public interface IDocumentoBaseCompraService
    {
        /// <summary>Borrador del destino con las líneas abiertas del origen. Lanza Exception con mensaje de usuario.</summary>
        Task<BorradorDocumentoBase> ObtenerBorradorAsync(int tipoOrigen, int entry, int tipoDestino);

        /// <summary>Valida las líneas con base de un destino que se va a crear (antes de numerar). No hace nada si no hay líneas.</summary>
        Task ValidarLineasAsync(int tipoDestino, string? codigoSnDestino, IReadOnlyList<LineaBaseRequest> lineas);

        /// <summary>Documentos destino activos que usan el origen (o solo una línea de él si noLinea no es null).</summary>
        Task<IReadOnlyList<DocumentoDerivado>> ObtenerDerivadosAsync(int tipoOrigen, int entry, int? noLinea);

        /// <summary>Lanza si el origen tiene derivados activos. accion: "cancelar el documento", "eliminar el documento", etc.</summary>
        Task ValidarOrigenModificableAsync(int tipoOrigen, int entry, string accion);

        /// <summary>Lanza si la línea del origen ya fue copiada a un documento activo. accion: "modificar" o "eliminar".</summary>
        Task ValidarLineaModificableAsync(int tipoOrigen, int entry, int noLinea, string accion);
    }
}
