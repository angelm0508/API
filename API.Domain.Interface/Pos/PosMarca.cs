namespace API.Domain.Interface.Pos
{
    public static class PosMarca
    {
        public const string Prefijo = "Venta POS - Cajero: ";
        public static string Comentario(string cajero) => Prefijo + cajero;
        public static bool EsVentaPos(string? comentario) => comentario != null && comentario.StartsWith(Prefijo, StringComparison.Ordinal);
        public static string Cajero(string? comentario) => EsVentaPos(comentario) ? comentario!.Substring(Prefijo.Length) : string.Empty;
    }
}
