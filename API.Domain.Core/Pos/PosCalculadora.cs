using System.Globalization;
using API.Domain.Interface.Pos;

namespace API.Domain.Core.Pos
{
    public sealed record LineaPosCalculada(string CodArticulo, decimal Cantidad, decimal PrecioNeto, decimal Bruto, decimal Impuesto, decimal TotalLinea);

    public sealed record TotalesPos(IReadOnlyList<LineaPosCalculada> Lineas, decimal Bruto, decimal Impuesto, decimal Total);

    public sealed record PagoPosRegistrado(string CodFormaPago, decimal Monto, string? Referencia, string? Banco, DateTime? FechaCheque);

    public sealed record ResultadoPagosPos(decimal Pagado, decimal Cambio, IReadOnlyList<PagoPosRegistrado> Medios);

    public static class PosCalculadora
    {
        public static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

        public static string Formato(decimal monto) => monto.ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>Una línea: bruto = cantidad × precio neto; impuesto sobre el bruto sin redondear (como la Web de facturas).</summary>
        public static LineaPosCalculada Linea(string codArticulo, decimal cantidad, decimal precioNeto, decimal tasaImpuesto)
        {
            var brutoSinRedondear = cantidad * precioNeto;
            var bruto = Redondear(brutoSinRedondear);
            var impuesto = Redondear(brutoSinRedondear * tasaImpuesto / 100m);
            return new LineaPosCalculada(codArticulo, cantidad, precioNeto, bruto, impuesto, bruto + impuesto);
        }

        public static TotalesPos Totales(IReadOnlyList<LineaPosCalculada> lineas) =>
            new(lineas, lineas.Sum(l => l.Bruto), lineas.Sum(l => l.Impuesto), lineas.Sum(l => l.TotalLinea));

        /// <summary>
        /// Valida los pagos contra el total y calcula el cambio. El cambio solo puede salir de la forma de pago efectivo y se
        /// resta de esos pagos en orden (los que quedan en cero se descartan): el cobro registrado es exactamente el total.
        /// </summary>
        public static ResultadoPagosPos ValidarPagos(decimal total, IReadOnlyList<VentaPosPago> pagos, string? formaEfectivo)
        {
            if (pagos == null || pagos.Count == 0)
                throw new Exception("Indica al menos una forma de pago.");
            if (pagos.Any(p => string.IsNullOrWhiteSpace(p.CodFormaPago)))
                throw new Exception("La forma de pago es requerida.");
            if (pagos.Any(p => Redondear(p.Monto) <= 0m))
                throw new Exception("El monto de cada forma de pago debe ser mayor que cero.");

            var montos = pagos.Select(p => Redondear(p.Monto)).ToList();
            var pagado = montos.Sum();
            if (pagado < total)
                throw new Exception($"El pago ({Formato(pagado)}) no cubre el total ({Formato(total)}).");

            var cambio = pagado - total;
            var efectivo = string.IsNullOrWhiteSpace(formaEfectivo)
                ? 0m
                : pagos.Select((p, i) => (p, i)).Where(x => x.p.CodFormaPago == formaEfectivo).Sum(x => montos[x.i]);
            if (cambio > efectivo)
                throw new Exception($"El cambio ({Formato(cambio)}) solo puede darse con efectivo.");

            var restante = cambio;
            var medios = new List<PagoPosRegistrado>();
            for (var i = 0; i < pagos.Count; i++)
            {
                var monto = montos[i];
                if (restante > 0m && pagos[i].CodFormaPago == formaEfectivo)
                {
                    var descuento = Math.Min(restante, monto);
                    monto -= descuento;
                    restante -= descuento;
                }
                if (monto > 0m)
                    medios.Add(new PagoPosRegistrado(pagos[i].CodFormaPago, monto, pagos[i].Referencia, pagos[i].Banco, pagos[i].FechaCheque));
            }
            return new ResultadoPagosPos(pagado, cambio, medios);
        }
    }
}
