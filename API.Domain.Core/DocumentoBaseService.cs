using System.Globalization;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    /// <summary>
    /// "Copiar a" de la cadena de ventas (Cotización → Pedido → Entrega → Factura), estilo SAP B1: cantidad abierta
    /// calculada en vivo, validación de las líneas copiadas e integridad del origen. Ver el spec
    /// 2026-10-18-copiar-a-documentos-venta-design.md.
    /// </summary>
    public class DocumentoBaseService : IDocumentoBaseService
    {
        private readonly IRepositorioGenerico<Cotizacion, int> _cotizacion;
        private readonly IRepositorioGenerico<CotizacionDetalle, (int Entry, int NoLinea)> _cotizacionDet;
        private readonly IRepositorioGenerico<Pedido, int> _pedido;
        private readonly IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)> _pedidoDet;
        private readonly IRepositorioGenerico<Entrega, int> _entrega;
        private readonly IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)> _entregaDet;
        private readonly IRepositorioGenerico<Factura, int> _factura;
        private readonly IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> _facturaDet;

        public DocumentoBaseService(
            IRepositorioGenerico<Cotizacion, int> cotizacion,
            IRepositorioGenerico<CotizacionDetalle, (int Entry, int NoLinea)> cotizacionDet,
            IRepositorioGenerico<Pedido, int> pedido,
            IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)> pedidoDet,
            IRepositorioGenerico<Entrega, int> entrega,
            IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)> entregaDet,
            IRepositorioGenerico<Factura, int> factura,
            IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> facturaDet)
        {
            _cotizacion = cotizacion;
            _cotizacionDet = cotizacionDet;
            _pedido = pedido;
            _pedidoDet = pedidoDet;
            _entrega = entrega;
            _entregaDet = entregaDet;
            _factura = factura;
            _facturaDet = facturaDet;
        }

        // ---------------------------------------------------------------------------------------------
        // Proyecciones comunes: las cuatro familias de tablas tienen las mismas columnas relevantes.
        // ---------------------------------------------------------------------------------------------

        private sealed record Cab(int Entry, int NumDoc, string? CodigoSn, string? NombreSn, string? Direccion, string? MonedaDoc,
            decimal? PrctjeDesc, decimal? PrctjeImpuesto, string? Comentario, string? Cancelado, string? EstadoDoc)
        {
            // Un documento está activo si no está cancelado ni por Cancelado ni por EstadoDoc.
            public bool Activo => Cancelado != "S" && EstadoDoc != "C";
            public string Etiqueta(int tipo) => $"{TipoDocumentoVenta.Nombre(tipo)} {NumDoc}";
        }

        private sealed record Lin(int Entry, int NoLinea, int? BaseTipo, int? BaseEntry, int? BaseLinea, string? CodArticulo, string? Descripcion,
            decimal? Cantidad, decimal? Precio, decimal? PrecioBruto, decimal? PrctjeDesc, string? CodigoImpuesto, decimal? Impuesto, string? CodAlmacen);

        private sealed record Consumo(int TipoDestino, int EntryDestino, int NumDocDestino, int BaseLinea, decimal Cantidad);

        private static Cab Mapear(Cotizacion h) => new(h.Entry, h.NumDoc, h.CodigoSn, h.NombreSn, h.Direccion, h.MonedaDoc, h.PrctjeDesc, h.PrctjeImpuesto, h.Comentario, h.Cancelado, h.EstadoDoc);
        private static Cab Mapear(Pedido h) => new(h.Entry, h.NumDoc, h.CodigoSn, h.NombreSn, h.Direccion, h.MonedaDoc, h.PrctjeDesc, h.PrctjeImpuesto, h.Comentario, h.Cancelado, h.EstadoDoc);
        private static Cab Mapear(Entrega h) => new(h.Entry, h.NumDoc, h.CodigoSn, h.NombreSn, h.Direccion, h.MonedaDoc, h.PrctjeDesc, h.PrctjeImpuesto, h.Comentario, h.Cancelado, h.EstadoDoc);
        private static Cab Mapear(Factura h) => new(h.Entry, h.NumDoc, h.CodigoSn, h.NombreSn, h.Direccion, h.MonedaDoc, h.PrctjeDesc, h.PrctjeImpuesto, h.Comentario, h.Cancelado, h.EstadoDoc);

        private static Lin Mapear(CotizacionDetalle d) => new(d.Entry, d.NoLinea, d.BaseTipo, d.BaseEntry, d.BaseLinea, d.CodArticulo, d.Descripcion, d.Cantidad, d.Precio, d.PrecioBruto, d.PrctjeDesc, d.CodigoImpuesto, d.Impuesto, d.CodAlmacen);
        private static Lin Mapear(PedidoDetalle d) => new(d.Entry, d.NoLinea, d.BaseTipo, d.BaseEntry, d.BaseLinea, d.CodArticulo, d.Descripcion, d.Cantidad, d.Precio, d.PrecioBruto, d.PrctjeDesc, d.CodigoImpuesto, d.Impuesto, d.CodAlmacen);
        private static Lin Mapear(EntregaDetalle d) => new(d.Entry, d.NoLinea, d.BaseTipo, d.BaseEntry, d.BaseLinea, d.CodArticulo, d.Descripcion, d.Cantidad, d.Precio, d.PrecioBruto, d.PrctjeDesc, d.CodigoImpuesto, d.Impuesto, d.CodAlmacen);
        private static Lin Mapear(FacturaDetalle d) => new(d.Entry, d.NoLinea, d.BaseTipo, d.BaseEntry, d.BaseLinea, d.CodArticulo, d.Descripcion, d.Cantidad, d.Precio, d.PrecioBruto, d.PrctjeDesc, d.CodigoImpuesto, d.Impuesto, d.CodAlmacen);

        // ---------------------------------------------------------------------------------------------
        // Lectura
        // ---------------------------------------------------------------------------------------------

        /// <summary>Cabecera del documento; null si no existe (o si una "factura" es en realidad una nota de débito).</summary>
        private async Task<Cab?> CabeceraAsync(int tipo, int entry)
        {
            switch (tipo)
            {
                case TipoDocumentoVenta.Cotizacion:
                    { var h = await _cotizacion.ObtenerAsync(entry); return h is null ? null : Mapear(h); }
                case TipoDocumentoVenta.Pedido:
                    { var h = await _pedido.ObtenerAsync(entry); return h is null ? null : Mapear(h); }
                case TipoDocumentoVenta.Entrega:
                    { var h = await _entrega.ObtenerAsync(entry); return h is null ? null : Mapear(h); }
                case TipoDocumentoVenta.Factura:
                    { var h = await _factura.ObtenerAsync(entry); return h is null || h.DocSubType != "--" ? null : Mapear(h); }
                default:
                    return null;
            }
        }

        /// <summary>Líneas de un documento, ordenadas por NoLinea.</summary>
        private async Task<List<Lin>> LineasDeAsync(int tipo, int entry)
        {
            switch (tipo)
            {
                case TipoDocumentoVenta.Cotizacion:
                    return (await (await _cotizacionDet.ObtenerTodoAsync()).Where(d => d.Entry == entry).ToListAsync()).Select(d => Mapear(d)).OrderBy(l => l.NoLinea).ToList();
                case TipoDocumentoVenta.Pedido:
                    return (await (await _pedidoDet.ObtenerTodoAsync()).Where(d => d.Entry == entry).ToListAsync()).Select(d => Mapear(d)).OrderBy(l => l.NoLinea).ToList();
                case TipoDocumentoVenta.Entrega:
                    return (await (await _entregaDet.ObtenerTodoAsync()).Where(d => d.Entry == entry).ToListAsync()).Select(d => Mapear(d)).OrderBy(l => l.NoLinea).ToList();
                case TipoDocumentoVenta.Factura:
                    return (await (await _facturaDet.ObtenerTodoAsync()).Where(d => d.Entry == entry).ToListAsync()).Select(d => Mapear(d)).OrderBy(l => l.NoLinea).ToList();
                default:
                    return new List<Lin>();
            }
        }

        /// <summary>Líneas de las tablas destino cuyo BaseTipo/BaseEntry apuntan al origen indicado.</summary>
        private async Task<List<Lin>> LineasConBaseAsync(int tipoDestino, int tipoOrigen, int entryOrigen)
        {
            switch (tipoDestino)
            {
                case TipoDocumentoVenta.Pedido:
                    return (await (await _pedidoDet.ObtenerTodoAsync()).Where(d => d.BaseTipo == tipoOrigen && d.BaseEntry == entryOrigen).ToListAsync()).Select(d => Mapear(d)).ToList();
                case TipoDocumentoVenta.Entrega:
                    return (await (await _entregaDet.ObtenerTodoAsync()).Where(d => d.BaseTipo == tipoOrigen && d.BaseEntry == entryOrigen).ToListAsync()).Select(d => Mapear(d)).ToList();
                case TipoDocumentoVenta.Factura:
                    return (await (await _facturaDet.ObtenerTodoAsync()).Where(d => d.BaseTipo == tipoOrigen && d.BaseEntry == entryOrigen).ToListAsync()).Select(d => Mapear(d)).ToList();
                default:
                    return new List<Lin>();
            }
        }

        /// <summary>Consumos (cantidad copiada por línea origen) de los documentos destino ACTIVOS.</summary>
        private async Task<List<Consumo>> ConsumosAsync(int tipoOrigen, int entry)
        {
            var consumos = new List<Consumo>();
            foreach (var destino in TipoDocumentoVenta.DestinosDe(tipoOrigen))
            {
                var lineas = await LineasConBaseAsync(destino, tipoOrigen, entry);
                foreach (var grupo in lineas.GroupBy(l => l.Entry))
                {
                    var cab = await CabeceraAsync(destino, grupo.Key);
                    if (cab is null || !cab.Activo) continue;
                    foreach (var l in grupo)
                        consumos.Add(new Consumo(destino, cab.Entry, cab.NumDoc, l.BaseLinea ?? 0, l.Cantidad ?? 0m));
                }
            }
            return consumos;
        }

        private static decimal Consumido(IEnumerable<Consumo> consumos, int noLinea) =>
            consumos.Where(c => c.BaseLinea == noLinea).Sum(c => c.Cantidad);

        private static string Fmt(decimal valor) => valor.ToString("0.######", CultureInfo.InvariantCulture);

        private static string Listar(IEnumerable<string> partes)
        {
            var lista = partes.ToList();
            return lista.Count <= 1
                ? string.Concat(lista)
                : string.Join(", ", lista.Take(lista.Count - 1)) + " y " + lista[^1];
        }

        // ---------------------------------------------------------------------------------------------
        // API del servicio
        // ---------------------------------------------------------------------------------------------

        public async Task<IReadOnlyList<DocumentoDerivado>> ObtenerDerivadosAsync(int tipoOrigen, int entry, int? noLinea = null)
        {
            var consumos = await ConsumosAsync(tipoOrigen, entry);
            return consumos
                .Where(c => c.Cantidad > 0m && (noLinea is null || c.BaseLinea == noLinea))
                .GroupBy(c => (c.TipoDestino, c.EntryDestino))
                .Select(g => new DocumentoDerivado(g.Key.TipoDestino, g.Key.EntryDestino, g.First().NumDocDestino, g.First().BaseLinea))
                .OrderBy(d => d.Tipo).ThenBy(d => d.Entry)
                .ToList();
        }

        public Task<BorradorDocumentoBase> ObtenerBorradorAsync(int tipoOrigen, int entry, int tipoDestino) =>
            throw new NotImplementedException("Task 3");

        public Task ValidarLineasAsync(int tipoDestino, string? codigoSnDestino, IReadOnlyList<LineaBaseRequest> lineas) =>
            throw new NotImplementedException("Task 3");

        public Task ValidarOrigenModificableAsync(int tipoOrigen, int entry, string accion) =>
            throw new NotImplementedException("Task 4");

        public Task ValidarLineaModificableAsync(int tipoOrigen, int entry, int noLinea, string accion) =>
            throw new NotImplementedException("Task 4");
    }
}
