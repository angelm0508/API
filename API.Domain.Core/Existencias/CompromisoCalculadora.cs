using API.Domain.Interface.Existencias;

namespace API.Domain.Core.Existencias
{
    /// <summary>Una línea de pedido activa con su consumo ya sumado (en la unidad de la línea).</summary>
    public sealed record LineaPedidoAbierta(string CodArticulo, string CodAlmacen, decimal CantidadLinea, decimal Consumido, decimal FactorUom);

    public static class CompromisoCalculadora
    {
        /// <summary>Compara pares sin distinguir mayúsculas (la base ignora mayúsculas).</summary>
        public static readonly IEqualityComparer<ParExistencia> Comparador = new ParComparador();

        /// <summary>Cantidad abierta en unidad base: máx(0, cantidad − consumido) × factor (factor ≤ 0 se trata como 1).</summary>
        public static decimal AbiertoBase(decimal cantidadLinea, decimal consumido, decimal factorUom)
        {
            var abierto = cantidadLinea - consumido;
            if (abierto <= 0m) return 0m;
            var factor = factorUom <= 0m ? 1m : factorUom;
            return abierto * factor;
        }

        /// <summary>Suma lo abierto por (artículo, almacén). Solo incluye pares con cantidad mayor que 0.</summary>
        public static Dictionary<ParExistencia, decimal> SumarPorPar(IEnumerable<LineaPedidoAbierta> lineas)
        {
            var resultado = new Dictionary<ParExistencia, decimal>(Comparador);
            foreach (var l in lineas)
            {
                var abierto = AbiertoBase(l.CantidadLinea, l.Consumido, l.FactorUom);
                if (abierto <= 0m) continue;
                var par = new ParExistencia(l.CodArticulo.Trim(), l.CodAlmacen.Trim());
                resultado[par] = resultado.TryGetValue(par, out var actual) ? actual + abierto : abierto;
            }
            return resultado;
        }

        private sealed class ParComparador : IEqualityComparer<ParExistencia>
        {
            public bool Equals(ParExistencia? x, ParExistencia? y) =>
                x is not null && y is not null
                && string.Equals(x.CodArticulo, y.CodArticulo, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.CodAlmacen, y.CodAlmacen, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode(ParExistencia p) =>
                HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(p.CodArticulo), StringComparer.OrdinalIgnoreCase.GetHashCode(p.CodAlmacen));
        }
    }
}
