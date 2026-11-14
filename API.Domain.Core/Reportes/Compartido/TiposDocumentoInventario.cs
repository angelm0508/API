using API.Domain.Interface.Reportes;

namespace API.Domain.Core.Reportes.Compartido
{
    /// <summary>
    /// Nombres de los tipos de documento que aparecen en <c>MovimientoInventario.TipoDoc</c>.
    /// Solo mueven inventario los dominios que llaman a <c>AsentarAsync</c>: Entrega (5), Factura y Nota de débito (6),
    /// Entrega de compra (12), Factura de compra y Nota de débito de compra (13), Entrada (59) y Salida (60) de mercancía
    /// y Orden de servicio (70). Las notas de crédito (8 y 15) no generan movimientos; «15» se nombra solo por completitud.
    /// </summary>
    public static class TiposDocumentoInventario
    {
        /// <summary>Opciones del filtro «Tipo de documento»: los documentos que realmente generan movimientos.</summary>
        public static IReadOnlyList<OpcionFiltro> Opciones { get; } = new[] { "5", "6", "12", "13", "59", "60", "70" }
            .Select(codigo => new OpcionFiltro(codigo, Nombre(codigo)))
            .ToArray();

        public static string Nombre(string tipoDoc) => tipoDoc switch
        {
            "5" => "Entrega",
            "6" => "Factura / Nota de débito",
            "12" => "Entrega de compra",
            "13" => "Factura de compra / Nota de débito de compra",
            "15" => "Nota de crédito de compra",
            "59" => "Entrada de mercancía",
            "60" => "Salida de mercancía",
            "70" => "Orden de servicio",
            _ => "Documento " + tipoDoc
        };
    }
}
