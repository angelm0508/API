using System.Globalization;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    /// <summary>
    /// "Copiar a" de la cadena de compras (Pedido de compra → Entrega de compra → Factura de compra): cantidad abierta calculada
    /// en vivo, validación de las líneas copiadas e integridad del origen. Ver el spec 2026-11-21-copiar-a-documentos-compra-design.md.
    /// </summary>
    public class DocumentoBaseCompraService : IDocumentoBaseCompraService
    {
        private readonly IRepositorioGenerico<PedidoCompra, int> _pedido;
        private readonly IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)> _pedidoDet;
        private readonly IRepositorioGenerico<EntregaCompra, int> _entrega;
        private readonly IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)> _entregaDet;
        private readonly IRepositorioGenerico<FacturaCompra, int> _factura;
        private readonly IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> _facturaDet;

        public DocumentoBaseCompraService(
            IRepositorioGenerico<PedidoCompra, int> pedido,
            IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)> pedidoDet,
            IRepositorioGenerico<EntregaCompra, int> entrega,
            IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)> entregaDet,
            IRepositorioGenerico<FacturaCompra, int> factura,
            IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> facturaDet)
        {
            _pedido = pedido;
            _pedidoDet = pedidoDet;
            _entrega = entrega;
            _entregaDet = entregaDet;
            _factura = factura;
            _facturaDet = facturaDet;
        }

        private sealed record Cab(int Entry, int NumDoc, string? CodigoSn, string? NombreSn, string? Direccion, string? MonedaDoc,
            decimal? PrctjeDesc, decimal? PrctjeImpuesto, string? Comentario, string? Cancelado, string? EstadoDoc, string? Nit)
        {
            /// <summary>Origen activo: ni cancelado ni cerrado.</summary>
            public bool Activo => Cancelado != "S" && EstadoDoc != "C";
            /// <summary>Un destino consume cantidad del origen mientras no esté cancelado (mismo criterio que «Pedido» de existencias).</summary>
            public bool Consume => Cancelado != "S";
            public string Etiqueta(int tipo) => $"{TipoDocumentoCompra.Nombre(tipo)} {NumDoc}";
        }

        private sealed record Lin(int Entry, int NoLinea, int? BaseTipo, int? BaseEntry, int? BaseLinea, string? CodArticulo, string? Descripcion,
            decimal? Cantidad, decimal? Precio, decimal? PrecioBruto, decimal? PrctjeDesc, string? CodigoImpuesto, decimal? Impuesto, string? CodAlmacen,
            int? UomEntry = null, string? UomCodigo = null, decimal FactorUom = 1m, decimal? CantidadBase = null);

        private sealed record Consumo(int TipoDestino, int EntryDestino, int NumDocDestino, int BaseLinea, decimal Cantidad);

        private static Cab Mapear(PedidoCompra h) => new(h.Entry, h.NumDoc, h.CodigoSn, h.NombreSn, h.Direccion, h.MonedaDoc, h.PrctjeDesc, h.PrctjeImpuesto, h.Comentario, h.Cancelado, h.EstadoDoc, h.Nit);
        private static Cab Mapear(EntregaCompra h) => new(h.Entry, h.NumDoc, h.CodigoSn, h.NombreSn, h.Direccion, h.MonedaDoc, h.PrctjeDesc, h.PrctjeImpuesto, h.Comentario, h.Cancelado, h.EstadoDoc, h.Nit);
        private static Cab Mapear(FacturaCompra h) => new(h.Entry, h.NumDoc, h.CodigoSn, h.NombreSn, h.Direccion, h.MonedaDoc, h.PrctjeDesc, h.PrctjeImpuesto, h.Comentario, h.Cancelado, h.EstadoDoc, h.Nit);

        private static Lin Mapear(PedidoCompraDetalle d) => new(d.Entry, d.NoLinea, d.BaseTipo, d.BaseEntry, d.BaseLinea, d.CodArticulo, d.Descripcion, d.Cantidad, d.Precio, d.PrecioBruto, d.PrctjeDesc, d.CodigoImpuesto, d.Impuesto, d.CodAlmacen, d.UomEntry, d.UomEntryNavigation?.Codigo, d.FactorUom, d.CantidadBase);
        private static Lin Mapear(EntregaCompraDetalle d) => new(d.Entry, d.NoLinea, d.BaseTipo, d.BaseEntry, d.BaseLinea, d.CodArticulo, d.Descripcion, d.Cantidad, d.Precio, d.PrecioBruto, d.PrctjeDesc, d.CodigoImpuesto, d.Impuesto, d.CodAlmacen, d.UomEntry, d.UomEntryNavigation?.Codigo, d.FactorUom, d.CantidadBase);
        private static Lin Mapear(FacturaCompraDetalle d) => new(d.Entry, d.NoLinea, d.BaseTipo, d.BaseEntry, d.BaseLinea, d.CodArticulo, d.Descripcion, d.Cantidad, d.Precio, d.PrecioBruto, d.PrctjeDesc, d.CodigoImpuesto, d.Impuesto, d.CodAlmacen, d.UomEntry, d.UomEntryNavigation?.Codigo, d.FactorUom, d.CantidadBase);

        /// <summary>Cabecera del documento; null si no existe (o si una «factura» es en realidad una nota de débito).</summary>
        private async Task<Cab?> CabeceraAsync(int tipo, int entry)
        {
            switch (tipo)
            {
                case TipoDocumentoCompra.PedidoCompra:
                    { var h = await _pedido.ObtenerAsync(entry); return h is null ? null : Mapear(h); }
                case TipoDocumentoCompra.EntregaCompra:
                    { var h = await _entrega.ObtenerAsync(entry); return h is null ? null : Mapear(h); }
                case TipoDocumentoCompra.FacturaCompra:
                    { var h = await _factura.ObtenerAsync(entry); return h is null || h.DocSubType != "--" ? null : Mapear(h); }
                default:
                    return null;
            }
        }

        /// <summary>Líneas de un origen (pedido o entrega de compra), ordenadas por NoLinea.</summary>
        private async Task<List<Lin>> LineasDeAsync(int tipo, int entry)
        {
            switch (tipo)
            {
                case TipoDocumentoCompra.PedidoCompra:
                    return (await (await _pedidoDet.ObtenerTodoAsync()).Where(d => d.Entry == entry).ToListAsync()).Select(d => Mapear(d)).OrderBy(l => l.NoLinea).ToList();
                case TipoDocumentoCompra.EntregaCompra:
                    return (await (await _entregaDet.ObtenerTodoAsync()).Where(d => d.Entry == entry).ToListAsync()).Select(d => Mapear(d)).OrderBy(l => l.NoLinea).ToList();
                default:
                    return new List<Lin>();
            }
        }

        /// <summary>Líneas de las tablas destino cuyo BaseTipo/BaseEntry apuntan al origen indicado.</summary>
        private async Task<List<Lin>> LineasConBaseAsync(int tipoDestino, int tipoOrigen, int entryOrigen)
        {
            switch (tipoDestino)
            {
                case TipoDocumentoCompra.EntregaCompra:
                    return (await (await _entregaDet.ObtenerTodoAsync()).Where(d => d.BaseTipo == tipoOrigen && d.BaseEntry == entryOrigen).ToListAsync()).Select(d => Mapear(d)).ToList();
                case TipoDocumentoCompra.FacturaCompra:
                    return (await (await _facturaDet.ObtenerTodoAsync()).Where(d => d.BaseTipo == tipoOrigen && d.BaseEntry == entryOrigen).ToListAsync()).Select(d => Mapear(d)).ToList();
                default:
                    return new List<Lin>();
            }
        }

        /// <summary>Consumos (cantidad copiada por línea origen) de los documentos destino que consumen (no cancelados).</summary>
        private async Task<List<Consumo>> ConsumosAsync(int tipoOrigen, int entry)
        {
            var consumos = new List<Consumo>();
            foreach (var destino in TipoDocumentoCompra.DestinosDe(tipoOrigen))
            {
                var lineas = await LineasConBaseAsync(destino, tipoOrigen, entry);
                foreach (var grupo in lineas.GroupBy(l => l.Entry))
                {
                    var cab = await CabeceraAsync(destino, grupo.Key);
                    if (cab is null || !cab.Consume) continue;
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

        public async Task<IReadOnlyList<DocumentoDerivado>> ObtenerDerivadosAsync(int tipoOrigen, int entry, int? noLinea)
        {
            var consumos = await ConsumosAsync(tipoOrigen, entry);
            return consumos
                .Where(c => c.Cantidad > 0m && (noLinea is null || c.BaseLinea == noLinea))
                .GroupBy(c => (c.TipoDestino, c.EntryDestino))
                .Select(g => new DocumentoDerivado(g.Key.TipoDestino, g.Key.EntryDestino, g.First().NumDocDestino, g.First().BaseLinea))
                .OrderBy(d => d.Tipo).ThenBy(d => d.Entry)
                .ToList();
        }

        public async Task<BorradorDocumentoBase> ObtenerBorradorAsync(int tipoOrigen, int entry, int tipoDestino)
        {
            if (!TipoDocumentoCompra.EsCombinacionValida(tipoOrigen, tipoDestino))
                throw new Exception($"No se puede copiar {TipoDocumentoCompra.Nombre(tipoOrigen)} a {TipoDocumentoCompra.Nombre(tipoDestino)}.");

            var cab = await CabeceraAsync(tipoOrigen, entry)
                ?? throw new Exception(DocumentoBaseMensajes.OrigenNoExiste);

            if (!cab.Activo)
                throw new Exception("El documento origen está cancelado.");

            var lineas = await LineasDeAsync(tipoOrigen, entry);
            var consumos = await ConsumosAsync(tipoOrigen, entry);

            var abiertas = new List<LineaAbiertaVenta>();
            foreach (var l in lineas)
            {
                var abierta = (l.Cantidad ?? 0m) - Consumido(consumos, l.NoLinea);
                if (abierta <= 0m) continue;
                var factor = l.FactorUom > 0m ? l.FactorUom : 1m;
                abiertas.Add(new LineaAbiertaVenta(tipoOrigen, entry, l.NoLinea, l.CodArticulo, l.Descripcion,
                    l.Cantidad ?? 0m, abierta, l.Precio, l.PrecioBruto, l.PrctjeDesc, l.CodigoImpuesto, l.Impuesto, l.CodAlmacen,
                    l.UomEntry, l.UomCodigo, factor, UnidadesLinea.CantidadBase(abierta, factor)));
            }

            if (abiertas.Count == 0)
                throw new Exception("El documento no tiene líneas abiertas.");

            return new BorradorDocumentoBase(tipoOrigen, entry, cab.NumDoc,
                new CabeceraBorrador(cab.CodigoSn, cab.NombreSn, cab.Direccion, cab.MonedaDoc, cab.PrctjeDesc, cab.PrctjeImpuesto, cab.Comentario, cab.Nit),
                abiertas);
        }

        public async Task ValidarLineasAsync(int tipoDestino, string? codigoSnDestino, IReadOnlyList<LineaBaseRequest> lineas)
        {
            foreach (var documento in lineas.GroupBy(l => (l.BaseTipo, l.BaseEntry)))
            {
                var (tipoOrigen, entryOrigen) = documento.Key;

                if (!TipoDocumentoCompra.EsCombinacionValida(tipoOrigen, tipoDestino))
                    throw new Exception($"No se puede copiar {TipoDocumentoCompra.Nombre(tipoOrigen)} a {TipoDocumentoCompra.Nombre(tipoDestino)}.");

                var cab = await CabeceraAsync(tipoOrigen, entryOrigen)
                    ?? throw new Exception($"El documento base {TipoDocumentoCompra.Nombre(tipoOrigen)} {entryOrigen} no existe.");
                var etiqueta = cab.Etiqueta(tipoOrigen);

                if (!cab.Activo)
                    throw new Exception($"El documento base {etiqueta} está cancelado.");

                if (!string.Equals(cab.CodigoSn, codigoSnDestino, StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"El proveedor del documento debe ser el del documento base {etiqueta} ({cab.CodigoSn}).");

                var origenLineas = await LineasDeAsync(tipoOrigen, entryOrigen);
                var consumos = await ConsumosAsync(tipoOrigen, entryOrigen);

                foreach (var porLinea in documento.GroupBy(l => l.BaseLinea))
                {
                    var origen = origenLineas.FirstOrDefault(x => x.NoLinea == porLinea.Key)
                        ?? throw new Exception($"La línea {porLinea.Key} de {etiqueta} no existe.");

                    foreach (var l in porLinea)
                    {
                        if (!string.Equals(l.CodArticulo, origen.CodArticulo, StringComparison.Ordinal))
                            throw new Exception($"El artículo de la línea {porLinea.Key} de {etiqueta} no se puede cambiar.");
                        if (l.Cantidad <= 0m)
                            throw new Exception($"La cantidad de la línea {porLinea.Key} de {etiqueta} debe ser mayor que cero.");
                    }

                    var abierta = (origen.Cantidad ?? 0m) - Consumido(consumos, porLinea.Key);
                    if (porLinea.Sum(l => l.Cantidad) > abierta)
                        throw new Exception($"La cantidad de la línea {porLinea.Key} de {etiqueta} excede la cantidad abierta ({Fmt(abierta)}).");
                }
            }
        }

        public async Task ValidarOrigenModificableAsync(int tipoOrigen, int entry, string accion)
        {
            var derivados = await ObtenerDerivadosAsync(tipoOrigen, entry, null);
            if (derivados.Count == 0) return;

            throw new Exception($"No se puede {accion}: lo usan {Listar(derivados.Select(d => d.Etiqueta))}. Cancele primero esos documentos.");
        }

        public async Task ValidarLineaModificableAsync(int tipoOrigen, int entry, int noLinea, string accion)
        {
            var derivados = await ObtenerDerivadosAsync(tipoOrigen, entry, noLinea);
            if (derivados.Count == 0) return;

            throw new Exception($"No se puede {accion} la línea {noLinea}: la usan {Listar(derivados.Select(d => d.Etiqueta))}. Cancele primero esos documentos.");
        }
    }
}
