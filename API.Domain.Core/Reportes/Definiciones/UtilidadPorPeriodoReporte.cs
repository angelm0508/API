using System.Globalization;
using API.Domain.Core.Reportes.Compartido;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;

namespace API.Domain.Core.Reportes.Definiciones
{
    /// <summary>
    /// Venta neta, costo y utilidad por mes, año o día. Una fila por (período, moneda). Las notas de crédito restan de la venta
    /// del período de su fecha y las notas de débito por concepto suman; el IVA nunca entra. Las líneas del período se agregan en memoria
    /// (tope de <see cref="ConsultaLineasVenta.MaxLineas"/> líneas).
    /// </summary>
    public sealed class UtilidadPorPeriodoReporte : IReporte
    {
        public const string CodigoReporte = "utilidad-por-periodo";

        private readonly ConsultaLineasVenta _consulta;
        private readonly IRepositorioGenerico<NotaCredito, int> _notas;
        private readonly IRepositorioGenerico<Factura, int> _facturas;

        public UtilidadPorPeriodoReporte(ConsultaLineasVenta consulta, IRepositorioGenerico<NotaCredito, int> notas, IRepositorioGenerico<Factura, int> facturas)
        {
            _consulta = consulta;
            _notas = notas;
            _facturas = facturas;
        }

        public ReporteDefinicion Definicion { get; } = new(
            Codigo: CodigoReporte,
            Nombre: "Utilidad por período",
            Descripcion: "Venta neta, costo y ganancia por mes, año o día. Las notas de crédito restan de la venta.",
            Familia: "Ventas",
            Modulo: "ReportesVentas",
            Filtros: new[]
            {
                new FiltroReporte("fechas", "Fechas", TipoFiltro.RangoFechas, Obligatorio: true),
                new FiltroReporte("periodo", "Período", TipoFiltro.Lista, Obligatorio: false, Opciones: new[]
                {
                    new OpcionFiltro("MES", "Mes"),
                    new OpcionFiltro("ANIO", "Año"),
                    new OpcionFiltro("DIA", "Día")
                }, ValorPorDefecto: "MES"),
                new FiltroReporte("cliente", "Cliente", TipoFiltro.Socio, SubTipo: "C")
            },
            Columnas: new[]
            {
                new ColumnaReporte("periodo", "Período", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("moneda", "Moneda", TipoColumna.Texto, Agrupable: true, VisiblePorDefecto: false),
                new ColumnaReporte("ventas", "Ventas netas", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("notasCredito", "Notas de crédito", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("ventaNeta", "Venta neta total", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("costo", "Costo", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("utilidad", "Utilidad", TipoColumna.Dinero, Totaliza: Totalizacion.Suma)
            });

        public async Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite)
        {
            var periodo = filtros.Texto("periodo") ?? "MES";
            var desde = filtros.FechaDesde("fechas");
            var hasta = filtros.FechaHastaExclusiva("fechas");
            var cliente = filtros.Texto("cliente");

            var lineas = await _consulta.ObtenerAsync(new FiltroLineasVenta(desde, hasta, cliente, null, null, null));
            var notas = await NotasCreditoNetas.ObtenerAsync(_notas, desde, hasta, cliente);
            var notasDebito = await NotasDebitoConceptoNetas.ObtenerAsync(_facturas, desde, hasta, cliente);

            var acumulado = new Dictionary<(string Periodo, string? Moneda), Acumulador>();
            Acumulador Obtener(DateTime? fecha, string? moneda)
            {
                var clave = (Etiqueta(fecha, periodo), moneda);
                if (!acumulado.TryGetValue(clave, out var a)) acumulado[clave] = a = new Acumulador();
                return a;
            }

            foreach (var l in lineas)
            {
                var a = Obtener(l.Fecha, l.Moneda);
                a.Ventas += l.VentaNeta;
                a.Costo += l.Costo;
            }
            // Las notas de débito por concepto suman a la venta con costo cero.
            foreach (var nd in notasDebito)
                Obtener(nd.Fecha, nd.Moneda).Ventas += nd.Importe;
            foreach (var n in notas)
                Obtener(n.Fecha, n.Moneda).NotasCredito += n.Importe;

            return acumulado
                .OrderBy(kv => kv.Key.Periodo, StringComparer.Ordinal)
                .ThenBy(kv => kv.Key.Moneda, StringComparer.Ordinal)
                .Take(limite)
                .Select(kv =>
                {
                    var ventaNeta = kv.Value.Ventas - kv.Value.NotasCredito;
                    return new FilaReporte
                    {
                        ["periodo"] = kv.Key.Periodo,
                        ["moneda"] = kv.Key.Moneda,
                        ["ventas"] = kv.Value.Ventas,
                        ["notasCredito"] = kv.Value.NotasCredito,
                        ["ventaNeta"] = ventaNeta,
                        ["costo"] = kv.Value.Costo,
                        ["utilidad"] = ventaNeta - kv.Value.Costo
                    };
                })
                .ToList();
        }

        private static string Etiqueta(DateTime? fecha, string periodo)
        {
            var f = fecha ?? DateTime.MinValue;
            var formato = periodo switch { "ANIO" => "yyyy", "DIA" => "yyyy-MM-dd", _ => "yyyy-MM" };
            return f.ToString(formato, CultureInfo.InvariantCulture);
        }

        private sealed class Acumulador
        {
            public decimal Ventas;
            public decimal NotasCredito;
            public decimal Costo;
        }
    }
}
