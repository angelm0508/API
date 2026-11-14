using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core.Reportes.Definiciones
{
    /// <summary>
    /// Notas de crédito (restan) y de débito (suman) emitidas, sin cancelados, con la factura a la que se refieren.
    /// La nota de débito vive en Factura con DocSubType 'ND'; ambas guardan en BaseTipo/BaseEntry la factura base
    /// (BaseTipo 6 = factura). Las facturas base se resuelven con una consulta por lotes, no una por fila.
    /// </summary>
    public sealed class NotasCreditoDebitoReporte : IReporte
    {
        public const string CodigoReporte = "notas-credito-debito";

        private const int TipoDocFactura = 6;
        private const int TamanoLote = 1000;

        private readonly IRepositorioGenerico<Factura, int> _facturas;
        private readonly IRepositorioGenerico<NotaCredito, int> _notasCredito;

        public NotasCreditoDebitoReporte(IRepositorioGenerico<Factura, int> facturas, IRepositorioGenerico<NotaCredito, int> notasCredito)
        {
            _facturas = facturas;
            _notasCredito = notasCredito;
        }

        public ReporteDefinicion Definicion { get; } = new(
            Codigo: CodigoReporte,
            Nombre: "Notas de crédito y débito emitidas",
            Descripcion: "Notas de crédito (restan) y de débito (suman), con la factura a la que se refieren.",
            Familia: "Ventas",
            Modulo: "ReportesVentas",
            Filtros: new[]
            {
                new FiltroReporte("fechas", "Fechas", TipoFiltro.RangoFechas),
                new FiltroReporte("cliente", "Cliente", TipoFiltro.Socio, SubTipo: "C"),
                new FiltroReporte("tipo", "Tipo", TipoFiltro.Lista, Opciones: new[]
                {
                    new OpcionFiltro("NC", "Nota de crédito"),
                    new OpcionFiltro("ND", "Nota de débito")
                })
            },
            Columnas: new[]
            {
                new ColumnaReporte("fecha", "Fecha", TipoColumna.Fecha, Agrupable: true),
                new ColumnaReporte("tipo", "Tipo", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("serie", "Serie", TipoColumna.Entero, VisiblePorDefecto: false),
                new ColumnaReporte("numDoc", "No.", TipoColumna.Entero),
                new ColumnaReporte("codigoSn", "Código cliente", TipoColumna.Texto, VisiblePorDefecto: false),
                new ColumnaReporte("nombreSn", "Cliente", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("facturaBase", "Factura base", TipoColumna.Entero),
                new ColumnaReporte("moneda", "Moneda", TipoColumna.Texto, Agrupable: true, VisiblePorDefecto: false),
                new ColumnaReporte("total", "Total", TipoColumna.Dinero, Totaliza: Totalizacion.Suma)
            });

        public async Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite)
        {
            var desde = filtros.FechaDesde("fechas");
            var hasta = filtros.FechaHastaExclusiva("fechas");
            var cliente = filtros.Texto("cliente");
            var tipo = filtros.Texto("tipo");

            var documentos = new List<Documento>();

            if (tipo is null or "ND")
            {
                var q = (await _facturas.ObtenerTodoAsync()).Where(f => f.Cancelado != "S" && f.DocSubType == "ND");
                if (desde is { } d1) q = q.Where(f => f.FechaDoc >= d1);
                if (hasta is { } h1) q = q.Where(f => f.FechaDoc < h1);
                if (cliente is not null) q = q.Where(f => f.CodigoSn == cliente);

                var notas = await q.OrderBy(f => f.FechaDoc).ThenBy(f => f.NumDoc).ThenBy(f => f.Entry).Take(limite)
                    .Select(f => new { f.FechaDoc, f.Serie, f.NumDoc, f.CodigoSn, f.NombreSn, f.MonedaDoc, f.TotalDoc, f.BaseTipo, f.BaseEntry })
                    .ToListAsync();

                documentos.AddRange(notas.Select(n => new Documento(n.FechaDoc, "Nota de débito", 1m, n.Serie, n.NumDoc, n.CodigoSn,
                    n.NombreSn, n.MonedaDoc, n.TotalDoc ?? 0m, n.BaseTipo == TipoDocFactura ? n.BaseEntry : null)));
            }

            if (tipo is null or "NC")
            {
                var q = (await _notasCredito.ObtenerTodoAsync()).Where(n => n.Cancelado != "S");
                if (desde is { } d2) q = q.Where(n => n.FechaDoc >= d2);
                if (hasta is { } h2) q = q.Where(n => n.FechaDoc < h2);
                if (cliente is not null) q = q.Where(n => n.CodigoSn == cliente);

                var notas = await q.OrderBy(n => n.FechaDoc).ThenBy(n => n.NumDoc).ThenBy(n => n.Entry).Take(limite)
                    .Select(n => new { n.FechaDoc, n.Serie, n.NumDoc, n.CodigoSn, n.NombreSn, n.MonedaDoc, n.TotalDoc, n.BaseTipo, n.BaseEntry })
                    .ToListAsync();

                documentos.AddRange(notas.Select(n => new Documento(n.FechaDoc, "Nota de crédito", -1m, n.Serie, n.NumDoc, n.CodigoSn,
                    n.NombreSn, n.MonedaDoc, n.TotalDoc ?? 0m, n.BaseTipo == TipoDocFactura ? n.BaseEntry : null)));
            }

            var seleccion = documentos
                .OrderBy(d => d.Fecha)
                .ThenBy(d => d.Tipo, StringComparer.Ordinal)
                .ThenBy(d => d.NumDoc)
                .Take(limite)
                .ToList();

            var numerosBase = await ResolverFacturasBaseAsync(
                seleccion.Where(d => d.BaseEntry is not null).Select(d => d.BaseEntry!.Value).Distinct().ToList());

            return seleccion.Select(d => new FilaReporte
            {
                ["fecha"] = d.Fecha,
                ["tipo"] = d.Tipo,
                ["serie"] = d.Serie,
                ["numDoc"] = d.NumDoc,
                ["codigoSn"] = d.CodigoSn,
                ["nombreSn"] = d.NombreSn,
                ["facturaBase"] = d.BaseEntry is { } e && numerosBase.TryGetValue(e, out var numero) ? (int?)numero : null,
                ["moneda"] = d.Moneda,
                ["total"] = d.Signo * d.Total
            }).ToList();
        }

        /// <summary>Número de documento de las facturas por Entry, leídas en lotes de 1,000 (una consulta por lote, no por fila).</summary>
        private async Task<Dictionary<int, int>> ResolverFacturasBaseAsync(IReadOnlyList<int> entries)
        {
            var resultado = new Dictionary<int, int>();
            if (entries.Count == 0) return resultado;

            var todas = await _facturas.ObtenerTodoAsync();
            for (var i = 0; i < entries.Count; i += TamanoLote)
            {
                var lote = entries.Skip(i).Take(TamanoLote).ToList();
                var facturas = await todas.Where(f => lote.Contains(f.Entry)).Select(f => new { f.Entry, f.NumDoc }).ToListAsync();
                foreach (var f in facturas) resultado[f.Entry] = f.NumDoc;
            }
            return resultado;
        }

        private sealed record Documento(DateTime? Fecha, string Tipo, decimal Signo, int Serie, int NumDoc, string? CodigoSn,
            string? NombreSn, string? Moneda, decimal Total, int? BaseEntry);
    }
}
