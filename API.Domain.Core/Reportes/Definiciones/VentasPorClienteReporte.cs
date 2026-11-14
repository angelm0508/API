using API.Domain.Core.Reportes.Compartido;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;

namespace API.Domain.Core.Reportes.Definiciones
{
    /// <summary>
    /// Ranking de clientes por venta neta total o utilidad. Una fila por (cliente, moneda). Las notas de crédito restan;
    /// el IVA nunca entra. Las líneas del período se agregan en memoria (tope de <see cref="ConsultaLineasVenta.MaxLineas"/> líneas).
    /// </summary>
    public sealed class VentasPorClienteReporte : IReporte
    {
        public const string CodigoReporte = "ventas-por-cliente";

        private readonly ConsultaLineasVenta _consulta;
        private readonly IRepositorioGenerico<NotaCredito, int> _notas;

        public VentasPorClienteReporte(ConsultaLineasVenta consulta, IRepositorioGenerico<NotaCredito, int> notas)
        {
            _consulta = consulta;
            _notas = notas;
        }

        public ReporteDefinicion Definicion { get; } = new(
            Codigo: CodigoReporte,
            Nombre: "Ventas por cliente",
            Descripcion: "Venta neta, costo y utilidad por cliente; las notas de crédito restan.",
            Familia: "Ventas",
            Modulo: "ReportesVentas",
            Filtros: new[]
            {
                new FiltroReporte("fechas", "Fechas", TipoFiltro.RangoFechas, Obligatorio: true),
                new FiltroReporte("cliente", "Cliente", TipoFiltro.Socio, SubTipo: "C"),
                new FiltroReporte("medirPor", "Medir por", TipoFiltro.Lista, Obligatorio: false, Opciones: new[]
                {
                    new OpcionFiltro("venta", "Venta neta total"),
                    new OpcionFiltro("utilidad", "Utilidad")
                }, ValorPorDefecto: "venta"),
                new FiltroReporte("primeros", "Mostrar los primeros", TipoFiltro.Numero)
            },
            Columnas: new[]
            {
                new ColumnaReporte("posicion", "#", TipoColumna.Entero),
                new ColumnaReporte("codigoSn", "Código", TipoColumna.Texto),
                new ColumnaReporte("nombreSn", "Cliente", TipoColumna.Texto),
                new ColumnaReporte("moneda", "Moneda", TipoColumna.Texto, VisiblePorDefecto: false),
                new ColumnaReporte("ventas", "Ventas netas", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("notasCredito", "Notas de crédito", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("ventaNeta", "Venta neta total", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("costo", "Costo", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("utilidad", "Utilidad", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("documentos", "Facturas", TipoColumna.Entero, Totaliza: Totalizacion.Suma)
            });

        public async Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite)
        {
            var primeros = FiltroPrimeros.Leer(filtros);
            var medirPor = filtros.Texto("medirPor") ?? "venta";
            var desde = filtros.FechaDesde("fechas");
            var hasta = filtros.FechaHastaExclusiva("fechas");
            var cliente = filtros.Texto("cliente");

            var lineas = await _consulta.ObtenerAsync(new FiltroLineasVenta(desde, hasta, cliente, null, null, null));
            var notas = await NotasCreditoNetas.ObtenerAsync(_notas, desde, hasta, cliente);

            var acumulado = new Dictionary<(string? Codigo, string? Moneda), Acumulador>();
            Acumulador Obtener(string? codigo, string? nombre, string? moneda)
            {
                if (!acumulado.TryGetValue((codigo, moneda), out var a)) acumulado[(codigo, moneda)] = a = new Acumulador();
                a.Nombre ??= nombre;
                return a;
            }

            foreach (var l in lineas)
            {
                var a = Obtener(l.CodigoSn, l.NombreSn, l.Moneda);
                a.Ventas += l.VentaNeta;
                a.Costo += l.Costo;
                a.Documentos.Add(l.DocEntry);
            }
            foreach (var n in notas)
                Obtener(n.CodigoSn, n.NombreSn, n.Moneda).NotasCredito += n.Importe;

            var agregados = acumulado.Select(kv => new
            {
                Codigo = kv.Key.Codigo,
                Moneda = kv.Key.Moneda,
                kv.Value.Nombre,
                kv.Value.Ventas,
                kv.Value.NotasCredito,
                VentaNeta = kv.Value.Ventas - kv.Value.NotasCredito,
                kv.Value.Costo,
                Utilidad = kv.Value.Ventas - kv.Value.NotasCredito - kv.Value.Costo,
                Documentos = kv.Value.Documentos.Count
            });

            var ordenados = medirPor == "utilidad"
                ? agregados.OrderByDescending(a => a.Utilidad)
                : agregados.OrderByDescending(a => a.VentaNeta);

            var seleccion = ordenados
                .ThenBy(a => a.Codigo, StringComparer.Ordinal)
                .ThenBy(a => a.Moneda, StringComparer.Ordinal)
                .AsEnumerable();
            if (primeros is { } n2) seleccion = seleccion.Take(n2);

            // La posición se asigna después de recortar.
            return seleccion.Take(limite).Select((a, i) => new FilaReporte
            {
                ["posicion"] = i + 1,
                ["codigoSn"] = a.Codigo,
                ["nombreSn"] = a.Nombre,
                ["moneda"] = a.Moneda,
                ["ventas"] = a.Ventas,
                ["notasCredito"] = a.NotasCredito,
                ["ventaNeta"] = a.VentaNeta,
                ["costo"] = a.Costo,
                ["utilidad"] = a.Utilidad,
                ["documentos"] = a.Documentos
            }).ToList();
        }

        private sealed class Acumulador
        {
            public string? Nombre;
            public decimal Ventas;
            public decimal NotasCredito;
            public decimal Costo;
            public HashSet<int> Documentos { get; } = new();
        }
    }
}
