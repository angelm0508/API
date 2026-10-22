namespace API.Domain.Interface
{
    /// <summary>
    /// Tipos de objeto de la cadena de ventas y qué origen puede copiarse a qué destino. Los números coinciden con
    /// NumeracionDocumento.CodigoObj / TipoObjeto de cada tabla y con el BaseTipo de las líneas.
    /// </summary>
    public static class TipoDocumentoVenta
    {
        public const int Cotizacion = 3;
        public const int Pedido = 4;
        public const int Entrega = 5;
        public const int Factura = 6;
        public const int OrdenServicio = 70;

        public static string Nombre(int tipo) => tipo switch
        {
            Cotizacion => "Cotización",
            Pedido => "Pedido",
            Entrega => "Entrega",
            Factura => "Factura",
            OrdenServicio => "Orden de servicio",
            _ => $"Documento {tipo}"
        };

        public static IReadOnlyList<int> DestinosDe(int origen) => origen switch
        {
            Cotizacion => new[] { Pedido, Entrega, Factura },
            Pedido => new[] { Entrega, Factura },
            Entrega => new[] { Factura },
            OrdenServicio => new[] { Factura },
            _ => Array.Empty<int>()
        };

        public static bool EsCombinacionValida(int origen, int destino) => DestinosDe(origen).Contains(destino);
    }

    public static class DocumentoBaseMensajes
    {
        public const string OrigenNoExiste = "El documento origen no existe.";
        public const string OrdenNoFacturable = "La orden de servicio debe estar Terminada o Entregada para facturarse.";
    }

    /// <summary>Línea de un documento destino que apunta a una línea de su documento origen.</summary>
    public sealed record LineaBaseRequest(int BaseTipo, int BaseEntry, int BaseLinea, string? CodArticulo, decimal Cantidad);

    /// <summary>Línea abierta de un origen, lista para cargarse en el formulario del destino.</summary>
    public sealed record LineaAbiertaVenta(
        int BaseTipo, int BaseEntry, int BaseLinea, string? CodArticulo, string? Descripcion,
        decimal CantidadOrigen, decimal CantidadAbierta, decimal? Precio, decimal? PrecioBruto, decimal? PrctjeDesc,
        string? CodigoImpuesto, decimal? Impuesto, string? CodAlmacen);

    public sealed record CabeceraBorrador(
        string? CodigoSn, string? NombreSn, string? Direccion, string? MonedaDoc,
        decimal? PrctjeDesc, decimal? PrctjeImpuesto, string? Comentario);

    public sealed record BorradorDocumentoBase(
        int OrigenTipo, int OrigenEntry, int OrigenNumDoc, CabeceraBorrador Cabecera, IReadOnlyList<LineaAbiertaVenta> Lineas);

    /// <summary>Documento destino activo que consume (en todo o en parte) un documento origen.</summary>
    public sealed record DocumentoDerivado(int Tipo, int Entry, int NumDoc, int BaseLinea)
    {
        public string Etiqueta => $"{TipoDocumentoVenta.Nombre(Tipo)} {NumDoc}";
    }

    public static class LineasBase
    {
        /// <summary>
        /// Toma las líneas de un destino que traen documento base (BaseTipo &gt; 0) y las convierte en solicitudes de
        /// validación. BaseTipo nulo o &lt;= 0 (-1 = ninguno) significa línea libre y se ignora.
        /// </summary>
        public static IReadOnlyList<LineaBaseRequest> Extraer(
            IEnumerable<(int? BaseTipo, int? BaseEntry, int? BaseLinea, string? CodArticulo, decimal? Cantidad)> lineas)
        {
            var resultado = new List<LineaBaseRequest>();
            foreach (var l in lineas)
            {
                if (l.BaseTipo is null or <= 0) continue;
                if (l.BaseEntry is null || l.BaseLinea is null)
                    throw new Exception("La línea con documento base debe indicar BaseEntry y BaseLinea.");
                resultado.Add(new LineaBaseRequest(l.BaseTipo.Value, l.BaseEntry.Value, l.BaseLinea.Value, l.CodArticulo, l.Cantidad ?? 0m));
            }
            return resultado;
        }
    }

    public interface IDocumentoBaseService
    {
        /// <summary>Borrador del destino con las líneas abiertas del origen. Lanza Exception con mensaje de usuario.</summary>
        Task<BorradorDocumentoBase> ObtenerBorradorAsync(int tipoOrigen, int entry, int tipoDestino);

        /// <summary>
        /// Valida las líneas con base de un destino que se va a crear (antes de numerar). No hace nada si no hay líneas.
        /// Lanza Exception con mensaje de usuario ante cualquier incumplimiento.
        /// </summary>
        Task ValidarLineasAsync(int tipoDestino, string? codigoSnDestino, IReadOnlyList<LineaBaseRequest> lineas);

        /// <summary>Documentos destino activos que usan el origen (o solo una línea de él si se indica noLinea).</summary>
        Task<IReadOnlyList<DocumentoDerivado>> ObtenerDerivadosAsync(int tipoOrigen, int entry, int? noLinea = null);

        /// <summary>Lanza si el origen tiene derivados activos. accion: "cancelar el documento", "eliminar el documento", etc.</summary>
        Task ValidarOrigenModificableAsync(int tipoOrigen, int entry, string accion);

        /// <summary>Lanza si la línea del origen ya fue copiada a un documento activo. accion: "modificar" o "eliminar".</summary>
        Task ValidarLineaModificableAsync(int tipoOrigen, int entry, int noLinea, string accion);
    }
}
