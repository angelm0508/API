using System.Globalization;

namespace API.Domain.Interface.Reportes
{
    /// <summary>Acceso tipado a los valores de los filtros ya validados por el motor.</summary>
    public sealed class FiltrosReporte
    {
        private readonly IReadOnlyDictionary<string, FiltroValor> _valores;

        public FiltrosReporte(IReadOnlyDictionary<string, FiltroValor> valores)
        {
            _valores = valores;
        }

        public static FiltrosReporte Vacio { get; } = new(new Dictionary<string, FiltroValor>());

        public string? Texto(string campo)
        {
            var v = Obtener(campo)?.Valor;
            return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        }

        public decimal? Numero(string campo) =>
            decimal.TryParse(Obtener(campo)?.Valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;

        public DateTime? Fecha(string campo) => ParseFecha(Obtener(campo)?.Valor);

        public DateTime? FechaDesde(string campo) => ParseFecha(Obtener(campo)?.Desde);

        /// <summary>
        /// Límite superior EXCLUSIVO de un rango: las 00:00 del día siguiente a "hasta". Para el usuario el rango es
        /// inclusivo en ambos extremos; en las consultas se usa «menor que» este valor.
        /// </summary>
        public DateTime? FechaHastaExclusiva(string campo) => ParseFecha(Obtener(campo)?.Hasta)?.AddDays(1);

        public static DateTime? ParseFecha(string? texto) =>
            DateTime.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f.Date : null;

        private FiltroValor? Obtener(string campo) => _valores.TryGetValue(campo, out var v) ? v : null;
    }
}
