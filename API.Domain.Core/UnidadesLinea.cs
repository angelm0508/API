using API.Domain.Interface;

namespace API.Domain.Core
{
    /// <summary>
    /// Conversión entre la unidad de una línea y la unidad base del artículo (el inventario siempre va en base).
    /// <c>factor</c> = unidades base por 1 unidad de la línea.
    /// </summary>
    public static class UnidadesLinea
    {
        private const int Decimales = 6; // escala de las columnas decimal(19,6)

        public static decimal CantidadBase(decimal cantidad, decimal factor) =>
            factor == 1m ? cantidad : Math.Round(cantidad * factor, Decimales, MidpointRounding.AwayFromZero);

        public static decimal PrecioBase(decimal precio, decimal factor) =>
            factor == 1m || factor <= 0m ? precio : Math.Round(precio / factor, Decimales, MidpointRounding.AwayFromZero);

        public static async Task<(int? UomEntry, decimal Factor, decimal CantidadBase)> ResolverLineaAsync(
            IUnidadMedidaService? servicio, string? codArticulo, int? uomEntry, decimal? cantidad)
        {
            var cant = cantidad ?? 0m;
            if (servicio is null || string.IsNullOrWhiteSpace(codArticulo))
                return (uomEntry, 1m, CantidadBase(cant, 1m));

            var uom = await servicio.ResolverAsync(codArticulo, uomEntry);
            return (uom.UomEntry, uom.Factor, CantidadBase(cant, uom.Factor));
        }
    }
}
