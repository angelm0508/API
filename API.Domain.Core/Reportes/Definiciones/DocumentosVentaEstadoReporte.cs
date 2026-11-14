using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core.Reportes.Definiciones
{
    /// <summary>
    /// Cotizaciones, pedidos, entregas y facturas con su estado. Incluye los cancelados (es el propósito del reporte).
    /// Estado (Ruling 8): Cancelado = 'S' gana; si no, EstadoDoc = 'C' es cerrado; si no, abierto.
    /// Las facturas incluyen DocSubType '--' y 'ND'.
    /// </summary>
    public sealed class DocumentosVentaEstadoReporte : IReporte
    {
        public const string CodigoReporte = "documentos-venta-estado";

        private readonly IRepositorioGenerico<Cotizacion, int> _cotizaciones;
        private readonly IRepositorioGenerico<Pedido, int> _pedidos;
        private readonly IRepositorioGenerico<Entrega, int> _entregas;
        private readonly IRepositorioGenerico<Factura, int> _facturas;

        public DocumentosVentaEstadoReporte(
            IRepositorioGenerico<Cotizacion, int> cotizaciones,
            IRepositorioGenerico<Pedido, int> pedidos,
            IRepositorioGenerico<Entrega, int> entregas,
            IRepositorioGenerico<Factura, int> facturas)
        {
            _cotizaciones = cotizaciones;
            _pedidos = pedidos;
            _entregas = entregas;
            _facturas = facturas;
        }

        public ReporteDefinicion Definicion { get; } = new(
            Codigo: CodigoReporte,
            Nombre: "Documentos de venta por estado",
            Descripcion: "Cotizaciones, pedidos, entregas y facturas con su estado (abierto, cerrado o cancelado).",
            Familia: "Ventas",
            Modulo: "ReportesVentas",
            Filtros: new[]
            {
                new FiltroReporte("fechas", "Fechas", TipoFiltro.RangoFechas),
                new FiltroReporte("cliente", "Cliente", TipoFiltro.Socio, SubTipo: "C"),
                new FiltroReporte("tipo", "Tipo", TipoFiltro.Lista, Opciones: new[]
                {
                    new OpcionFiltro("COT", "Cotización"),
                    new OpcionFiltro("PED", "Pedido"),
                    new OpcionFiltro("ENT", "Entrega"),
                    new OpcionFiltro("FAC", "Factura")
                }),
                new FiltroReporte("estado", "Estado", TipoFiltro.Lista, Opciones: new[]
                {
                    new OpcionFiltro("ABIERTO", "Abierto"),
                    new OpcionFiltro("CERRADO", "Cerrado"),
                    new OpcionFiltro("CANCELADO", "Cancelado")
                })
            },
            Columnas: new[]
            {
                new ColumnaReporte("tipo", "Tipo", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("fecha", "Fecha", TipoColumna.Fecha, Agrupable: true),
                new ColumnaReporte("numDoc", "No.", TipoColumna.Entero),
                new ColumnaReporte("codigoSn", "Código cliente", TipoColumna.Texto, VisiblePorDefecto: false),
                new ColumnaReporte("nombreSn", "Cliente", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("estado", "Estado", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("moneda", "Moneda", TipoColumna.Texto, Agrupable: true, VisiblePorDefecto: false),
                new ColumnaReporte("total", "Total", TipoColumna.Dinero, Totaliza: Totalizacion.Suma)
            });

        public async Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite)
        {
            var criterio = new Criterio(
                filtros.FechaDesde("fechas"), filtros.FechaHastaExclusiva("fechas"), filtros.Texto("cliente"), filtros.Texto("estado"));
            var tipo = filtros.Texto("tipo");

            var documentos = new List<DocVenta>();

            if (tipo is null or "COT")
            {
                var q = (await _cotizaciones.ObtenerTodoAsync()).Select(d => new DocVenta
                {
                    Tipo = "Cotización", Fecha = d.FechaDoc, NumDoc = d.NumDoc, CodigoSn = d.CodigoSn, NombreSn = d.NombreSn,
                    Cancelado = d.Cancelado, EstadoDoc = d.EstadoDoc, Moneda = d.MonedaDoc, Total = d.TotalDoc
                });
                documentos.AddRange(await LeerAsync(q, criterio, limite));
            }

            if (tipo is null or "PED")
            {
                var q = (await _pedidos.ObtenerTodoAsync()).Select(d => new DocVenta
                {
                    Tipo = "Pedido", Fecha = d.FechaDoc, NumDoc = d.NumDoc, CodigoSn = d.CodigoSn, NombreSn = d.NombreSn,
                    Cancelado = d.Cancelado, EstadoDoc = d.EstadoDoc, Moneda = d.MonedaDoc, Total = d.TotalDoc
                });
                documentos.AddRange(await LeerAsync(q, criterio, limite));
            }

            if (tipo is null or "ENT")
            {
                var q = (await _entregas.ObtenerTodoAsync()).Select(d => new DocVenta
                {
                    Tipo = "Entrega", Fecha = d.FechaDoc, NumDoc = d.NumDoc, CodigoSn = d.CodigoSn, NombreSn = d.NombreSn,
                    Cancelado = d.Cancelado, EstadoDoc = d.EstadoDoc, Moneda = d.MonedaDoc, Total = d.TotalDoc
                });
                documentos.AddRange(await LeerAsync(q, criterio, limite));
            }

            if (tipo is null or "FAC")
            {
                var q = (await _facturas.ObtenerTodoAsync()).Where(d => d.DocSubType == "--" || d.DocSubType == "ND").Select(d => new DocVenta
                {
                    Tipo = "Factura", Fecha = d.FechaDoc, NumDoc = d.NumDoc, CodigoSn = d.CodigoSn, NombreSn = d.NombreSn,
                    Cancelado = d.Cancelado, EstadoDoc = d.EstadoDoc, Moneda = d.MonedaDoc, Total = d.TotalDoc
                });
                documentos.AddRange(await LeerAsync(q, criterio, limite));
            }

            return documentos
                .OrderBy(d => d.Fecha)
                .ThenBy(d => d.Tipo, StringComparer.Ordinal)
                .ThenBy(d => d.NumDoc)
                .Take(limite)
                .Select(d => new FilaReporte
                {
                    ["tipo"] = d.Tipo,
                    ["fecha"] = d.Fecha,
                    ["numDoc"] = d.NumDoc,
                    ["codigoSn"] = d.CodigoSn,
                    ["nombreSn"] = d.NombreSn,
                    ["estado"] = Estado(d.Cancelado, d.EstadoDoc),
                    ["moneda"] = d.Moneda,
                    ["total"] = d.Total ?? 0m
                })
                .ToList();
        }

        /// <summary>Aplica los filtros comunes (incluido el estado, también a los cancelados) y limita cada fuente.</summary>
        private static async Task<List<DocVenta>> LeerAsync(IQueryable<DocVenta> q, Criterio c, int limite)
        {
            if (c.Desde is { } desde) q = q.Where(d => d.Fecha >= desde);
            if (c.Hasta is { } hasta) q = q.Where(d => d.Fecha < hasta);
            if (c.Cliente is { } cliente) q = q.Where(d => d.CodigoSn == cliente);
            q = c.Estado switch
            {
                "CANCELADO" => q.Where(d => d.Cancelado == "S"),
                "CERRADO" => q.Where(d => d.Cancelado != "S" && d.EstadoDoc == "C"),
                "ABIERTO" => q.Where(d => d.Cancelado != "S" && d.EstadoDoc != "C"),
                _ => q
            };

            return await q.OrderBy(d => d.Fecha).ThenBy(d => d.NumDoc).Take(limite).ToListAsync();
        }

        private static string Estado(string? cancelado, string? estadoDoc) =>
            cancelado == "S" ? "Cancelado" : estadoDoc == "C" ? "Cerrado" : "Abierto";

        private sealed record Criterio(DateTime? Desde, DateTime? Hasta, string? Cliente, string? Estado);

        private sealed class DocVenta
        {
            public string Tipo { get; set; } = "";
            public DateTime? Fecha { get; set; }
            public int NumDoc { get; set; }
            public string? CodigoSn { get; set; }
            public string? NombreSn { get; set; }
            public string? Cancelado { get; set; }
            public string? EstadoDoc { get; set; }
            public string? Moneda { get; set; }
            public decimal? Total { get; set; }
        }
    }
}
