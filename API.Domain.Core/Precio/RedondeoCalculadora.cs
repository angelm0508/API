namespace API.Domain.Core.Precio
{
    /// <summary>
    /// Redondeo de precios según la configuración de una lista de precio.
    /// <para>metodo: 0/null = sin múltiplo, 1 = 1, 2 = 0.5, 3 = 5, 4 = 10, 5 = 25.</para>
    /// <para>regla: "F" hacia abajo, "C" hacia arriba, "R"/null/otro = al más cercano.</para>
    /// </summary>
    public static class RedondeoCalculadora
    {
        public static decimal Redondear(decimal valor, short? metodo, string? regla)
        {
            decimal paso = (metodo ?? 0) switch
            {
                1 => 1m,
                2 => 0.5m,
                3 => 5m,
                4 => 10m,
                5 => 25m,
                _ => 0m
            };

            decimal resultado;
            if (paso == 0m)
            {
                resultado = valor;
            }
            else
            {
                var q = valor / paso;
                var qRedondeado = (regla ?? "R").Trim().ToUpperInvariant() switch
                {
                    "F" => Math.Floor(q),
                    "C" => Math.Ceiling(q),
                    _ => Math.Round(q, MidpointRounding.AwayFromZero)
                };
                resultado = qRedondeado * paso;
            }

            return Math.Round(resultado, 2, MidpointRounding.AwayFromZero);
        }
    }
}
