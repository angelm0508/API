namespace API.Domain.Interface
{
    /// <summary>
    /// Cálculos puros de la ficha de stock por almacén. El 0 se trata como "sin nivel" (como SAP B1),
    /// por eso mínimo y máximo solo cuentan cuando son mayores que 0.
    /// </summary>
    public static class StockAlmacenCalculadora
    {
        public const string Ok = "OK";
        public const string BajoMinimo = "BAJO_MINIMO";
        public const string SobreMaximo = "SOBRE_MAXIMO";

        public static decimal Disponible(decimal enStock, decimal comprometido, decimal pedido) =>
            enStock - comprometido + pedido;

        public static decimal? NivelNecesario(decimal? maximo, decimal disponible) =>
            maximo is > 0 ? Math.Max(0m, maximo.Value - disponible) : null;

        public static string Estado(decimal enStock, decimal? minimo, decimal? maximo) =>
            minimo is > 0 && enStock < minimo.Value ? BajoMinimo
            : maximo is > 0 && enStock > maximo.Value ? SobreMaximo
            : Ok;
    }
}
