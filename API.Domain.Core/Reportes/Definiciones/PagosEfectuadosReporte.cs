using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core.Reportes.Definiciones
{
    /// <summary>
    /// Pagos efectuados a proveedores, sin cancelados: una fila por medio de pago (<see cref="PagoEfectuadoMedio"/>).
    /// <c>facturasAplicadas</c> lista los números de documento de las facturas de compra del pago (<see cref="PagoEfectuadoFactura"/>),
    /// ordenados y separados por coma; es el mismo texto para todas las filas (medios) de un pago. Las aplicaciones se leen
    /// por lotes de 1,000 pagos y las facturas con una consulta por lote de entries, no una por pago.
    /// </summary>
    public sealed class PagosEfectuadosReporte : IReporte
    {
        public const string CodigoReporte = "pagos-efectuados";

        private const int TamanoLote = 1000;

        private readonly IRepositorioGenerico<PagoEfectuado, int> _pagos;
        private readonly IRepositorioGenerico<PagoEfectuadoMedio, (int Entry, int NoLinea)> _medios;
        private readonly IRepositorioGenerico<PagoEfectuadoFactura, (int Entry, int NoLinea)> _aplicaciones;
        private readonly IRepositorioGenerico<FacturaCompra, int> _facturas;

        public PagosEfectuadosReporte(
            IRepositorioGenerico<PagoEfectuado, int> pagos,
            IRepositorioGenerico<PagoEfectuadoMedio, (int Entry, int NoLinea)> medios,
            IRepositorioGenerico<PagoEfectuadoFactura, (int Entry, int NoLinea)> aplicaciones,
            IRepositorioGenerico<FacturaCompra, int> facturas)
        {
            _pagos = pagos;
            _medios = medios;
            _aplicaciones = aplicaciones;
            _facturas = facturas;
        }

        public ReporteDefinicion Definicion { get; } = new(
            Codigo: CodigoReporte,
            Nombre: "Pagos efectuados",
            Descripcion: "Pagos a proveedores por forma de pago, con las facturas de compra a las que se aplicaron.",
            Familia: "Cobros y pagos",
            Modulo: "ReportesCobrosPagos",
            Filtros: new[]
            {
                new FiltroReporte("fechas", "Fechas", TipoFiltro.RangoFechas),
                new FiltroReporte("proveedor", "Proveedor", TipoFiltro.Socio, SubTipo: "P"),
                new FiltroReporte("formaPago", "Forma de pago (código)", TipoFiltro.Texto)
            },
            Columnas: new[]
            {
                new ColumnaReporte("fecha", "Fecha", TipoColumna.Fecha, Agrupable: true),
                new ColumnaReporte("serie", "Serie", TipoColumna.Entero, VisiblePorDefecto: false),
                new ColumnaReporte("numDoc", "No.", TipoColumna.Entero),
                new ColumnaReporte("codigoSn", "Código proveedor", TipoColumna.Texto, VisiblePorDefecto: false),
                new ColumnaReporte("nombreSn", "Proveedor", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("formaPago", "Forma de pago", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("referencia", "Referencia", TipoColumna.Texto),
                new ColumnaReporte("banco", "Banco", TipoColumna.Texto, VisiblePorDefecto: false),
                new ColumnaReporte("monto", "Monto", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("facturasAplicadas", "Facturas aplicadas", TipoColumna.Texto)
            });

        public async Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite)
        {
            var desde = filtros.FechaDesde("fechas");
            var hasta = filtros.FechaHastaExclusiva("fechas");
            var proveedor = filtros.Texto("proveedor");
            var formaPago = filtros.Texto("formaPago");

            var pagos = (await _pagos.ObtenerTodoAsync()).Where(p => p.Cancelado != "S");
            if (desde is { } d) pagos = pagos.Where(p => p.FechaDoc >= d);
            if (hasta is { } h) pagos = pagos.Where(p => p.FechaDoc < h);
            if (proveedor is not null) pagos = pagos.Where(p => p.CodigoSn == proveedor);

            var medios = await _medios.ObtenerTodoAsync();
            if (formaPago is not null) medios = medios.Where(m => m.CodFormaPago == formaPago);

            var filas = await (
                from m in medios
                join p in pagos on m.Entry equals p.Entry
                orderby p.FechaDoc, p.NumDoc, p.Entry, m.NoLinea
                select new
                {
                    p.Entry,
                    p.FechaDoc,
                    p.Serie,
                    p.NumDoc,
                    p.CodigoSn,
                    p.NombreSn,
                    m.CodFormaPago,
                    NombreForma = m.CodFormaPagoNavigation != null ? m.CodFormaPagoNavigation.Nombre : null,
                    m.Referencia,
                    m.Banco,
                    m.Monto
                })
                .Take(limite)
                .ToListAsync();

            var textos = await ResolverFacturasAplicadasAsync(filas.Select(f => f.Entry).Distinct().ToList());

            return filas.Select(f => new FilaReporte
            {
                ["fecha"] = f.FechaDoc,
                ["serie"] = f.Serie,
                ["numDoc"] = f.NumDoc,
                ["codigoSn"] = f.CodigoSn,
                ["nombreSn"] = f.NombreSn,
                ["formaPago"] = f.NombreForma ?? f.CodFormaPago,
                ["referencia"] = f.Referencia,
                ["banco"] = f.Banco,
                ["monto"] = f.Monto,
                ["facturasAplicadas"] = textos.TryGetValue(f.Entry, out var texto) ? texto : ""
            }).ToList();
        }

        /// <summary>Texto «12, 15, 20» por Entry de pago: aplicaciones por lotes y una consulta de facturas por lote de entries.</summary>
        private async Task<Dictionary<int, string>> ResolverFacturasAplicadasAsync(IReadOnlyList<int> pagoEntries)
        {
            var textos = new Dictionary<int, string>();
            if (pagoEntries.Count == 0) return textos;

            var todasAplicaciones = await _aplicaciones.ObtenerTodoAsync();
            var aplicadas = new List<(int Pago, int Factura)>();
            for (var i = 0; i < pagoEntries.Count; i += TamanoLote)
            {
                var lote = pagoEntries.Skip(i).Take(TamanoLote).ToList();
                var filas = await todasAplicaciones.Where(a => lote.Contains(a.Entry)).Select(a => new { a.Entry, a.FacturaCompraEntry }).ToListAsync();
                aplicadas.AddRange(filas.Select(a => (a.Entry, a.FacturaCompraEntry)));
            }
            if (aplicadas.Count == 0) return textos;

            var facturaEntries = aplicadas.Select(a => a.Factura).Distinct().ToList();
            var todasFacturas = await _facturas.ObtenerTodoAsync();
            var numeros = new Dictionary<int, int>();
            for (var i = 0; i < facturaEntries.Count; i += TamanoLote)
            {
                var lote = facturaEntries.Skip(i).Take(TamanoLote).ToList();
                var facturas = await todasFacturas.Where(f => lote.Contains(f.Entry)).Select(f => new { f.Entry, f.NumDoc }).ToListAsync();
                foreach (var f in facturas) numeros[f.Entry] = f.NumDoc;
            }

            foreach (var grupo in aplicadas.GroupBy(a => a.Pago))
            {
                var lista = grupo.Select(a => a.Factura).Distinct().Where(numeros.ContainsKey).Select(e => numeros[e]).OrderBy(n => n).ToList();
                textos[grupo.Key] = string.Join(", ", lista);
            }
            return textos;
        }
    }
}
