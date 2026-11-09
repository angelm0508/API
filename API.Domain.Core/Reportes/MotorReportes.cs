using System.Globalization;
using API.Domain.Interface.Reportes;

namespace API.Domain.Core.Reportes
{
    /// <summary>
    /// Motor único de todos los reportes: valida la solicitud contra la definición, ejecuta, ordena y totaliza.
    /// Solo acepta filtros, columnas y campos de orden que la definición declara (no hay SQL libre).
    /// </summary>
    public sealed class MotorReportes : IMotorReportes
    {
        public const int LimiteFilas = 50000;
        public const int MaxCriteriosOrden = 3;
        public const int MaxNivelesAgrupacion = 2;

        private const string CampoMoneda = "moneda";
        private const int MaxLongitudTexto = 100;

        public async Task<ResultadoReporte> EjecutarAsync(IReporte reporte, SolicitudReporte solicitud)
        {
            if (solicitud is null)
                throw new ReporteInvalidoException("La solicitud del reporte es obligatoria.");

            var definicion = reporte.Definicion;
            var filtros = ValidarFiltros(definicion, solicitud.Filtros);
            var visibles = ResolverColumnas(definicion, solicitud.Columnas);
            ValidarOrden(definicion, solicitud.Orden);

            var filas = (await reporte.EjecutarAsync(filtros, LimiteFilas + 1)).ToList();
            var truncado = filas.Count > LimiteFilas;
            if (truncado) filas = filas.Take(LimiteFilas).ToList();

            filas = Ordenar(filas, solicitud.Orden);

            var resultado = new ResultadoReporte { Truncado = truncado, TotalFilas = filas.Count };

            var sumarImportes = true;
            if (VariasMonedas(filas))
            {
                sumarImportes = false;
                var colMoneda = definicion.Columnas.FirstOrDefault(c => c.Campo == CampoMoneda);
                if (colMoneda is not null && !visibles.Contains(colMoneda)) visibles.Add(colMoneda);
                resultado.Avisos.Add("El resultado incluye más de una moneda: los importes no se suman.");
            }

            resultado.Columnas = visibles;
            resultado.Totales = Totalizar(filas, visibles, sumarImportes);
            resultado.Filas = Proyectar(filas, visibles);
            return resultado;
        }

        // ---------- validación ----------

        private static FiltrosReporte ValidarFiltros(ReporteDefinicion definicion, Dictionary<string, FiltroValor>? recibidos)
        {
            var limpios = new Dictionary<string, FiltroValor>(StringComparer.Ordinal);

            foreach (var (campo, valor) in recibidos ?? new Dictionary<string, FiltroValor>())
            {
                var filtro = definicion.Filtros.FirstOrDefault(f => f.Campo == campo)
                    ?? throw new ReporteInvalidoException($"El filtro '{campo}' no es válido para este reporte.");

                if (valor is null || EstaVacio(filtro, valor)) continue;   // vacío = filtro no aplicado
                ValidarValor(filtro, valor);
                limpios[campo] = valor;
            }

            foreach (var filtro in definicion.Filtros.Where(f => f.Obligatorio))
                if (!limpios.ContainsKey(filtro.Campo))
                    throw new ReporteInvalidoException($"El filtro '{filtro.Titulo}' es obligatorio.");

            return new FiltrosReporte(limpios);
        }

        private static bool EstaVacio(FiltroReporte filtro, FiltroValor valor) =>
            filtro.Tipo == TipoFiltro.RangoFechas
                ? string.IsNullOrWhiteSpace(valor.Desde) && string.IsNullOrWhiteSpace(valor.Hasta)
                : string.IsNullOrWhiteSpace(valor.Valor);

        private static void ValidarValor(FiltroReporte filtro, FiltroValor valor)
        {
            var invalido = new ReporteInvalidoException($"El valor del filtro '{filtro.Titulo}' no es válido.");

            switch (filtro.Tipo)
            {
                case TipoFiltro.RangoFechas:
                    var desde = FiltrosReporte.ParseFecha(valor.Desde);
                    var hasta = FiltrosReporte.ParseFecha(valor.Hasta);
                    if ((!string.IsNullOrWhiteSpace(valor.Desde) && desde is null) || (!string.IsNullOrWhiteSpace(valor.Hasta) && hasta is null))
                        throw invalido;
                    if (desde is not null && hasta is not null && desde > hasta)
                        throw new ReporteInvalidoException($"El rango del filtro '{filtro.Titulo}' no es válido: la fecha inicial es posterior a la final.");
                    break;
                case TipoFiltro.Fecha:
                    if (FiltrosReporte.ParseFecha(valor.Valor) is null) throw invalido;
                    break;
                case TipoFiltro.Numero:
                    if (!decimal.TryParse(valor.Valor, NumberStyles.Number, CultureInfo.InvariantCulture, out _)) throw invalido;
                    break;
                case TipoFiltro.Lista:
                    if (filtro.Opciones is null || !filtro.Opciones.Any(o => o.Valor == valor.Valor)) throw invalido;
                    break;
                default:   // Texto, Socio, Articulo, Almacen
                    if (valor.Valor!.Length > MaxLongitudTexto) throw invalido;
                    break;
            }
        }

        private static List<ColumnaReporte> ResolverColumnas(ReporteDefinicion definicion, List<string>? pedidas)
        {
            if (pedidas is null || pedidas.Count == 0)
                return definicion.Columnas.Where(c => c.VisiblePorDefecto).ToList();

            var resultado = new List<ColumnaReporte>();
            foreach (var campo in pedidas)
            {
                var columna = definicion.Columnas.FirstOrDefault(c => c.Campo == campo)
                    ?? throw new ReporteInvalidoException($"La columna '{campo}' no es válida para este reporte.");
                if (!resultado.Contains(columna)) resultado.Add(columna);
            }
            return resultado;
        }

        private static void ValidarOrden(ReporteDefinicion definicion, List<OrdenReporte>? orden)
        {
            if (orden is null || orden.Count == 0) return;
            if (orden.Count > MaxCriteriosOrden)
                throw new ReporteInvalidoException($"Solo se permiten hasta {MaxCriteriosOrden} criterios de orden.");

            foreach (var o in orden)
                if (!definicion.Columnas.Any(c => c.Campo == o.Campo))
                    throw new ReporteInvalidoException($"No se puede ordenar por '{o.Campo}': la columna no es válida para este reporte.");
        }

        // ---------- cálculo ----------

        private static List<FilaReporte> Ordenar(List<FilaReporte> filas, List<OrdenReporte>? orden)
        {
            if (orden is null || orden.Count == 0) return filas;

            IOrderedEnumerable<FilaReporte>? ordenado = null;
            foreach (var o in orden)
            {
                var campo = o.Campo;
                object? Clave(FilaReporte f) => f.TryGetValue(campo, out var v) ? v : null;

                ordenado = (ordenado, o.Descendente) switch
                {
                    (null, false) => filas.OrderBy(f => Clave(f), ComparadorValores.Instancia),
                    (null, true) => filas.OrderByDescending(f => Clave(f), ComparadorValores.Instancia),
                    (_, false) => ordenado!.ThenBy(f => Clave(f), ComparadorValores.Instancia),
                    (_, true) => ordenado!.ThenByDescending(f => Clave(f), ComparadorValores.Instancia)
                };
            }
            return ordenado!.ToList();
        }

        private static bool VariasMonedas(List<FilaReporte> filas) =>
            filas.Select(f => f.TryGetValue(CampoMoneda, out var m) ? m as string : null)
                 .Where(m => !string.IsNullOrEmpty(m))
                 .Distinct(StringComparer.OrdinalIgnoreCase)
                 .Count() > 1;

        private static Dictionary<string, decimal?> Totalizar(IReadOnlyCollection<FilaReporte> filas, IEnumerable<ColumnaReporte> visibles, bool sumarImportes)
        {
            var totales = new Dictionary<string, decimal?>();
            foreach (var col in visibles.Where(c => c.Totaliza != Totalizacion.Ninguna))
            {
                if (!sumarImportes && col.Tipo == TipoColumna.Dinero)
                {
                    totales[col.Campo] = null;
                    continue;
                }

                var valores = filas.Select(f => f.TryGetValue(col.Campo, out var v) ? v : null).Where(v => v is not null).ToList();
                totales[col.Campo] = col.Totaliza switch
                {
                    Totalizacion.Suma => (decimal?)valores.Sum(v => ADecimal(v!)),
                    Totalizacion.Promedio => valores.Count == 0 ? null : (decimal?)valores.Average(v => ADecimal(v!)),
                    Totalizacion.Conteo => (decimal?)valores.Count,
                    _ => null
                };
            }
            return totales;
        }

        private static List<FilaReporte> Proyectar(IEnumerable<FilaReporte> filas, IReadOnlyList<ColumnaReporte> visibles) =>
            filas.Select(f =>
            {
                var nueva = new FilaReporte();
                foreach (var c in visibles) nueva[c.Campo] = f.TryGetValue(c.Campo, out var v) ? v : null;
                return nueva;
            }).ToList();

        private static decimal ADecimal(object valor) => Convert.ToDecimal(valor, CultureInfo.InvariantCulture);
    }

    /// <summary>Compara valores de celdas: nulos primero, números como decimal, fechas como fecha y el resto como texto sin distinguir mayúsculas.</summary>
    internal sealed class ComparadorValores : IComparer<object?>
    {
        public static readonly ComparadorValores Instancia = new();

        public int Compare(object? x, object? y)
        {
            if (x is null && y is null) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            if (EsNumero(x) && EsNumero(y))
                return Convert.ToDecimal(x, CultureInfo.InvariantCulture).CompareTo(Convert.ToDecimal(y, CultureInfo.InvariantCulture));
            if (x is DateTime dx && y is DateTime dy) return dx.CompareTo(dy);
            return StringComparer.CurrentCultureIgnoreCase.Compare(x.ToString(), y.ToString());
        }

        public static bool EsNumero(object valor) =>
            valor is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
    }
}
