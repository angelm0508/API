using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core.Reportes.Definiciones
{
    /// <summary>
    /// Ventas por documento: facturas y notas de débito (suman) y notas de crédito (restan), sin cancelados.
    /// La nota de débito vive en Factura con DocSubType 'ND'.
    /// </summary>
    public sealed class VentasPorDocumentoReporte : IReporte
    {
        public const string CodigoReporte = "ventas-por-documento";

        private readonly IRepositorioGenerico<Factura, int> _facturas;
        private readonly IRepositorioGenerico<NotaCredito, int> _notasCredito;

        public VentasPorDocumentoReporte(IRepositorioGenerico<Factura, int> facturas, IRepositorioGenerico<NotaCredito, int> notasCredito)
        {
            _facturas = facturas;
            _notasCredito = notasCredito;
        }

        public ReporteDefinicion Definicion { get; } = new(
            Codigo: CodigoReporte,
            Nombre: "Ventas por documento",
            Descripcion: "Facturas y notas de débito menos notas de crédito, sin documentos cancelados.",
            Familia: "Ventas",
            Modulo: "ReportesVentas",
            Filtros: new[]
            {
                new FiltroReporte("fechas", "Fechas", TipoFiltro.RangoFechas),
                new FiltroReporte("cliente", "Cliente", TipoFiltro.Socio, SubTipo: "C"),
                new FiltroReporte("tipo", "Tipo", TipoFiltro.Lista, Opciones: new[]
                {
                    new OpcionFiltro("FAC", "Factura"),
                    new OpcionFiltro("ND", "Nota de débito"),
                    new OpcionFiltro("NC", "Nota de crédito")
                }),
                new FiltroReporte("serie", "Serie", TipoFiltro.Numero)
            },
            Columnas: new[]
            {
                new ColumnaReporte("fecha", "Fecha", TipoColumna.Fecha, Agrupable: true),
                new ColumnaReporte("tipo", "Tipo", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("serie", "Serie", TipoColumna.Entero, Agrupable: true, VisiblePorDefecto: false),
                new ColumnaReporte("numDoc", "No.", TipoColumna.Entero),
                new ColumnaReporte("codigoSn", "Código cliente", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("nombreSn", "Cliente", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("moneda", "Moneda", TipoColumna.Texto, Agrupable: true, VisiblePorDefecto: false),
                new ColumnaReporte("bruto", "Bruto", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("descuento", "Descuento", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("impuesto", "Impuesto", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("total", "Total", TipoColumna.Dinero, Totaliza: Totalizacion.Suma)
            });

        public async Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite)
        {
            var desde = filtros.FechaDesde("fechas");
            var hasta = filtros.FechaHastaExclusiva("fechas");
            var cliente = filtros.Texto("cliente");
            var tipo = filtros.Texto("tipo");
            var serie = filtros.Numero("serie") is { } s ? (int?)s : null;

            var filas = new List<FilaReporte>();

            if (tipo is null or "FAC" or "ND")
            {
                var q = (await _facturas.ObtenerTodoAsync()).Where(f => f.Cancelado != "S");
                q = tipo switch
                {
                    "FAC" => q.Where(f => f.DocSubType == "--"),
                    "ND" => q.Where(f => f.DocSubType == "ND"),
                    _ => q.Where(f => f.DocSubType == "--" || f.DocSubType == "ND")
                };
                if (desde is { } d1) q = q.Where(f => f.FechaDoc >= d1);
                if (hasta is { } h1) q = q.Where(f => f.FechaDoc < h1);
                if (cliente is not null) q = q.Where(f => f.CodigoSn == cliente);
                if (serie is { } s1) q = q.Where(f => f.Serie == s1);

                var documentos = await q.OrderBy(f => f.FechaDoc).ThenBy(f => f.Entry).Take(limite)
                    .Select(f => new { f.FechaDoc, f.DocSubType, f.Serie, f.NumDoc, f.CodigoSn, f.NombreSn, f.MonedaDoc, f.TotalBruto, f.TotalDesc, f.TotalImp, f.TotalDoc })
                    .ToListAsync();

                filas.AddRange(documentos.Select(x => Fila(x.FechaDoc, x.DocSubType == "ND" ? "Nota de débito" : "Factura", 1m,
                    x.Serie, x.NumDoc, x.CodigoSn, x.NombreSn, x.MonedaDoc, x.TotalBruto, x.TotalDesc, x.TotalImp, x.TotalDoc)));
            }

            if (tipo is null or "NC")
            {
                var q = (await _notasCredito.ObtenerTodoAsync()).Where(n => n.Cancelado != "S");
                if (desde is { } d2) q = q.Where(n => n.FechaDoc >= d2);
                if (hasta is { } h2) q = q.Where(n => n.FechaDoc < h2);
                if (cliente is not null) q = q.Where(n => n.CodigoSn == cliente);
                if (serie is { } s2) q = q.Where(n => n.Serie == s2);

                var notas = await q.OrderBy(n => n.FechaDoc).ThenBy(n => n.Entry).Take(limite)
                    .Select(n => new { n.FechaDoc, n.Serie, n.NumDoc, n.CodigoSn, n.NombreSn, n.MonedaDoc, n.TotalBruto, n.TotalDesc, n.TotalImp, n.TotalDoc })
                    .ToListAsync();

                filas.AddRange(notas.Select(x => Fila(x.FechaDoc, "Nota de crédito", -1m,
                    x.Serie, x.NumDoc, x.CodigoSn, x.NombreSn, x.MonedaDoc, x.TotalBruto, x.TotalDesc, x.TotalImp, x.TotalDoc)));
            }

            return filas
                .OrderBy(f => (DateTime?)f["fecha"])
                .ThenBy(f => (string)f["tipo"]!, StringComparer.Ordinal)
                .Take(limite)
                .ToList();
        }

        private static FilaReporte Fila(DateTime? fecha, string tipo, decimal signo, int serie, int numDoc, string? codigoSn, string? nombreSn,
            string? moneda, decimal? bruto, decimal? descuento, decimal? impuesto, decimal? total) =>
            new()
            {
                ["fecha"] = fecha,
                ["tipo"] = tipo,
                ["serie"] = serie,
                ["numDoc"] = numDoc,
                ["codigoSn"] = codigoSn,
                ["nombreSn"] = nombreSn,
                ["moneda"] = moneda,
                ["bruto"] = signo * (bruto ?? 0m),
                ["descuento"] = signo * (descuento ?? 0m),
                ["impuesto"] = signo * (impuesto ?? 0m),
                ["total"] = signo * (total ?? 0m)
            };
    }
}
