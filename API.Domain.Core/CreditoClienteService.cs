using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    /// <summary>
    /// Posición de crédito de los clientes: saldo y vencido (de ISaldoSocioService) más el valor comprometido en pedidos y
    /// entregas abiertos. Una línea copiada a un documento destino activo (Entrega o Factura con BaseTipo/BaseEntry/BaseLinea)
    /// deja de contar en el origen; ver DocumentoBaseService.ConsumosAsync. Cargas agrupadas: una consulta por tabla.
    /// </summary>
    public class CreditoClienteService : ICreditoClienteService
    {
        private const string TipoCliente = "C";

        private readonly IRepositorioGenerico<SocioNegocio, string> _repoSocio;
        private readonly IRepositorioGenerico<Pedido, int> _repoPedido;
        private readonly IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)> _repoPedidoDet;
        private readonly IRepositorioGenerico<Entrega, int> _repoEntrega;
        private readonly IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)> _repoEntregaDet;
        private readonly IRepositorioGenerico<Factura, int> _repoFactura;
        private readonly IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> _repoFacturaDet;
        private readonly ISaldoSocioService _saldoSocio;

        public CreditoClienteService(
            IRepositorioGenerico<SocioNegocio, string> repoSocio,
            IRepositorioGenerico<Pedido, int> repoPedido,
            IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)> repoPedidoDet,
            IRepositorioGenerico<Entrega, int> repoEntrega,
            IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)> repoEntregaDet,
            IRepositorioGenerico<Factura, int> repoFactura,
            IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> repoFacturaDet,
            ISaldoSocioService saldoSocio)
        {
            _repoSocio = repoSocio;
            _repoPedido = repoPedido;
            _repoPedidoDet = repoPedidoDet;
            _repoEntrega = repoEntrega;
            _repoEntregaDet = repoEntregaDet;
            _repoFactura = repoFactura;
            _repoFacturaDet = repoFacturaDet;
            _saldoSocio = saldoSocio;
        }

        // ---------------------------------------------------------------------------------------------
        // Valor abierto de documentos origen (pedidos y entregas)
        // ---------------------------------------------------------------------------------------------

        /// <summary>Documento origen (pedido o entrega) del que se quiere calcular el valor abierto.</summary>
        private sealed record CabeceraOrigen(int Tipo, int Entry, string? CodigoSn, decimal? TotalDoc);

        /// <summary>
        /// Línea de un origen con su cantidad abierta (cantidad menos lo copiado a documentos destino activos) y su valor neto:
        /// ValorUnitario = TotalLinea × factor / Cantidad; ValorAbierto = Abierta × ValorUnitario (0 si no hay cantidad abierta).
        /// </summary>
        private sealed record LineaValor(int NoLinea, decimal Cantidad, decimal Abierta, decimal ValorUnitario, decimal ValorAbierto);

        /// <summary>Origen con su factor (TotalDoc / Σ TotalLinea) y sus líneas valoradas.</summary>
        private sealed record OrigenValor(int Tipo, int Entry, string? CodigoSn, decimal Factor, IReadOnlyList<LineaValor> Lineas)
        {
            /// <summary>Valor abierto del documento, redondeado a 2 decimales.</summary>
            public decimal Valor => Redondear(Lineas.Sum(l => l.ValorAbierto));
        }

        private sealed record LineaOrigenDb(int Entry, int NoLinea, decimal Cantidad, decimal TotalLinea);

        /// <summary>
        /// Carga, para un conjunto de orígenes (pedidos tipo 4 y entregas tipo 5), sus líneas, el factor y lo consumido por los
        /// destinos activos. Una consulta por tabla: PedidoDetalle, EntregaDetalle (líneas de los orígenes y líneas que copian
        /// pedidos), FacturaDetalle (líneas con base pedido o entrega) y, para conocer qué destinos están activos, las
        /// cabeceras de Factura y de las Entrega consumidoras que el llamador no conozca.
        /// </summary>
        /// <param name="origenes">Orígenes con su cabecera ya leída (sin importar si están cancelados).</param>
        /// <param name="entregasConocidas">Estado activo (Entry → activa) de entregas que el llamador ya leyó; evita volver a consultarlas.</param>
        private async Task<Dictionary<(int Tipo, int Entry), OrigenValor>> CargarOrigenesAsync(
            IReadOnlyCollection<CabeceraOrigen> origenes,
            IReadOnlyDictionary<int, bool>? entregasConocidas = null)
        {
            var resultado = new Dictionary<(int Tipo, int Entry), OrigenValor>();
            if (origenes.Count == 0)
                return resultado;

            var pedidoIds = origenes.Where(o => o.Tipo == TipoDocumentoVenta.Pedido).Select(o => o.Entry).Distinct().ToList();
            var entregaIds = origenes.Where(o => o.Tipo == TipoDocumentoVenta.Entrega).Select(o => o.Entry).Distinct().ToList();
            if (pedidoIds.Count == 0 && entregaIds.Count == 0)
                return resultado;

            // (1) Líneas de los orígenes.
            var lineasPedido = new List<LineaOrigenDb>();
            if (pedidoIds.Count > 0)
            {
                lineasPedido = (await (await _repoPedidoDet.ObtenerTodoAsync())
                        .Where(d => pedidoIds.Contains(d.Entry))
                        .Select(d => new { d.Entry, d.NoLinea, d.Cantidad, d.TotalLinea })
                        .ToListAsync())
                    .Select(d => new LineaOrigenDb(d.Entry, d.NoLinea, d.Cantidad ?? 0m, d.TotalLinea ?? 0m))
                    .ToList();
            }

            // Una sola consulta a EntregaDetalle: las líneas de las entregas origen y las líneas que copian pedidos (BaseTipo 4).
            var lineasEntrega = new List<LineaOrigenDb>();
            var entregasQueCopianPedidos = new List<(int Entry, int BaseEntry, int BaseLinea, decimal Cantidad)>();
            if (pedidoIds.Count > 0 || entregaIds.Count > 0)
            {
                var filas = await (await _repoEntregaDet.ObtenerTodoAsync())
                    .Where(d => entregaIds.Contains(d.Entry)
                                || (d.BaseTipo == TipoDocumentoVenta.Pedido && d.BaseEntry != null && pedidoIds.Contains(d.BaseEntry.Value)))
                    .Select(d => new { d.Entry, d.NoLinea, d.BaseTipo, d.BaseEntry, d.BaseLinea, d.Cantidad, d.TotalLinea })
                    .ToListAsync();
                var entregaIdSet = entregaIds.ToHashSet();
                foreach (var f in filas)
                {
                    if (entregaIdSet.Contains(f.Entry))
                        lineasEntrega.Add(new LineaOrigenDb(f.Entry, f.NoLinea, f.Cantidad ?? 0m, f.TotalLinea ?? 0m));
                    if (f.BaseTipo == TipoDocumentoVenta.Pedido && f.BaseEntry != null && pedidoIds.Contains(f.BaseEntry.Value))
                        entregasQueCopianPedidos.Add((f.Entry, f.BaseEntry.Value, f.BaseLinea ?? 0, f.Cantidad ?? 0m));
                }
            }

            // Líneas de factura que copian pedidos (BaseTipo 4) o entregas (BaseTipo 5), sea cual sea el DocSubType de la factura.
            var facturasQueCopian = (await (await _repoFacturaDet.ObtenerTodoAsync())
                    .Where(d => d.BaseEntry != null
                                && ((d.BaseTipo == TipoDocumentoVenta.Pedido && pedidoIds.Contains(d.BaseEntry.Value))
                                    || (d.BaseTipo == TipoDocumentoVenta.Entrega && entregaIds.Contains(d.BaseEntry.Value))))
                    .Select(d => new { d.Entry, d.BaseTipo, d.BaseEntry, d.BaseLinea, d.Cantidad })
                    .ToListAsync())
                .Select(d => (d.Entry, BaseTipo: d.BaseTipo!.Value, BaseEntry: d.BaseEntry!.Value, BaseLinea: d.BaseLinea ?? 0, Cantidad: d.Cantidad ?? 0m))
                .ToList();

            // (2) Solo consumen los destinos activos (Cancelado != 'S' y EstadoDoc != 'C').
            var entregasActivas = new HashSet<int>();
            var entregasConsumidoras = entregasQueCopianPedidos.Select(l => l.Entry).Distinct().ToList();
            var porConsultar = new List<int>();
            foreach (var entry in entregasConsumidoras)
            {
                if (entregasConocidas != null && entregasConocidas.TryGetValue(entry, out var activa))
                {
                    if (activa) entregasActivas.Add(entry);
                }
                else
                    porConsultar.Add(entry);
            }
            if (porConsultar.Count > 0)
            {
                var cabeceras = await (await _repoEntrega.ObtenerTodoAsync())
                    .Where(e => porConsultar.Contains(e.Entry) && e.Cancelado != "S" && e.EstadoDoc != "C")
                    .Select(e => e.Entry)
                    .ToListAsync();
                entregasActivas.UnionWith(cabeceras);
            }

            var facturasActivas = new HashSet<int>();
            var facturasConsumidoras = facturasQueCopian.Select(l => l.Entry).Distinct().ToList();
            if (facturasConsumidoras.Count > 0)
            {
                var cabeceras = await (await _repoFactura.ObtenerTodoAsync())
                    .Where(f => facturasConsumidoras.Contains(f.Entry) && f.Cancelado != "S" && f.EstadoDoc != "C")
                    .Select(f => f.Entry)
                    .ToListAsync();
                facturasActivas.UnionWith(cabeceras);
            }

            // (3) Cantidad copiada por línea origen (tipo, entry, noLinea).
            var consumido = new Dictionary<(int Tipo, int Entry, int NoLinea), decimal>();
            void Consumir(int tipo, int entry, int noLinea, decimal cantidad)
            {
                consumido.TryGetValue((tipo, entry, noLinea), out var actual);
                consumido[(tipo, entry, noLinea)] = actual + cantidad;
            }
            foreach (var l in entregasQueCopianPedidos.Where(l => entregasActivas.Contains(l.Entry)))
                Consumir(TipoDocumentoVenta.Pedido, l.BaseEntry, l.BaseLinea, l.Cantidad);
            foreach (var l in facturasQueCopian.Where(l => facturasActivas.Contains(l.Entry)))
                Consumir(l.BaseTipo, l.BaseEntry, l.BaseLinea, l.Cantidad);

            // (4) Valor de cada origen.
            var lineasPorOrigen = lineasPedido.Select(l => (Tipo: TipoDocumentoVenta.Pedido, Linea: l))
                .Concat(lineasEntrega.Select(l => (Tipo: TipoDocumentoVenta.Entrega, Linea: l)))
                .ToLookup(x => (x.Tipo, x.Linea.Entry), x => x.Linea);
            foreach (var origen in origenes)
            {
                if (resultado.ContainsKey((origen.Tipo, origen.Entry)))
                    continue;
                var lineas = lineasPorOrigen[(origen.Tipo, origen.Entry)].ToList();
                var sumaLineas = lineas.Sum(l => l.TotalLinea);
                var factor = sumaLineas == 0m ? 1m : (origen.TotalDoc ?? sumaLineas) / sumaLineas;
                var valoradas = lineas.OrderBy(l => l.NoLinea).Select(l =>
                {
                    consumido.TryGetValue((origen.Tipo, origen.Entry, l.NoLinea), out var copiado);
                    var abierta = l.Cantidad - copiado;
                    if (l.Cantidad <= 0m)
                        return new LineaValor(l.NoLinea, l.Cantidad, 0m, 0m, 0m);
                    var unitario = l.TotalLinea * factor / l.Cantidad;
                    var valorAbierto = abierta <= 0m ? 0m : abierta / l.Cantidad * l.TotalLinea * factor;
                    return new LineaValor(l.NoLinea, l.Cantidad, abierta <= 0m ? 0m : abierta, unitario, valorAbierto);
                }).ToList();
                resultado[(origen.Tipo, origen.Entry)] = new OrigenValor(origen.Tipo, origen.Entry, origen.CodigoSn, factor, valoradas);
            }
            return resultado;
        }

        // ---------------------------------------------------------------------------------------------
        // API del servicio
        // ---------------------------------------------------------------------------------------------

        public async Task<PosicionCredito?> ObtenerPosicionAsync(string codigoSn)
        {
            if (string.IsNullOrEmpty(codigoSn))
                return null;
            var posiciones = await ObtenerPosicionesAsync(new[] { codigoSn });
            return posiciones.TryGetValue(codigoSn, out var posicion) ? posicion : null;
        }

        public async Task<IReadOnlyDictionary<string, PosicionCredito>> ObtenerPosicionesAsync(IEnumerable<string> codigosSn)
        {
            var resultado = new Dictionary<string, PosicionCredito>(StringComparer.OrdinalIgnoreCase);
            var codigos = (codigosSn ?? Enumerable.Empty<string>())
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (codigos.Count == 0)
                return resultado;

            var socios = await (await _repoSocio.ObtenerTodoAsync())
                .Where(s => codigos.Contains(s.Codigo) && s.TipoSn == TipoCliente)
                .Select(s => new { s.Codigo, s.LimiteCredito, s.BloquearSiVencido })
                .ToListAsync();
            if (socios.Count == 0)
                return resultado;

            var clientes = socios.Select(s => s.Codigo).ToList();
            var saldos = await _saldoSocio.ObtenerAsync(clientes);

            // Cabeceras de pedidos activos y de entregas del cliente (de las entregas se guarda también si están activas,
            // para saber si consumen líneas de pedido sin volver a consultarlas).
            var pedidos = await (await _repoPedido.ObtenerTodoAsync())
                .Where(p => p.CodigoSn != null && clientes.Contains(p.CodigoSn) && p.Cancelado != "S" && p.EstadoDoc != "C")
                .Select(p => new { p.Entry, p.CodigoSn, p.TotalDoc })
                .ToListAsync();
            var entregas = await (await _repoEntrega.ObtenerTodoAsync())
                .Where(e => e.CodigoSn != null && clientes.Contains(e.CodigoSn))
                .Select(e => new { e.Entry, e.CodigoSn, e.TotalDoc, e.Cancelado, e.EstadoDoc })
                .ToListAsync();

            var entregasConocidas = entregas.ToDictionary(e => e.Entry, e => e.Cancelado != "S" && e.EstadoDoc != "C");
            var origenes = pedidos.Select(p => new CabeceraOrigen(TipoDocumentoVenta.Pedido, p.Entry, p.CodigoSn, p.TotalDoc))
                .Concat(entregas.Where(e => entregasConocidas[e.Entry])
                    .Select(e => new CabeceraOrigen(TipoDocumentoVenta.Entrega, e.Entry, e.CodigoSn, e.TotalDoc)))
                .ToList();
            var valores = await CargarOrigenesAsync(origenes, entregasConocidas);

            // Por cliente: suma de los valores (ya redondeados por documento).
            var comprometido = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in valores.Values)
            {
                comprometido.TryGetValue(o.CodigoSn!, out var actual);
                comprometido[o.CodigoSn!] = actual + o.Valor;
            }

            foreach (var socio in socios)
            {
                saldos.TryGetValue(socio.Codigo, out var saldo);
                comprometido.TryGetValue(socio.Codigo, out var comp);
                comp = Redondear(comp);
                var exposicion = (saldo?.Saldo ?? 0m) + comp;
                resultado[socio.Codigo] = new PosicionCredito(socio.Codigo, socio.LimiteCredito, socio.BloquearSiVencido == "S",
                    saldo?.Saldo ?? 0m, saldo?.Vencido ?? 0m, comp, exposicion,
                    socio.LimiteCredito.HasValue ? socio.LimiteCredito.Value - exposicion : null);
            }
            return resultado;
        }

        public async Task<decimal> ValorAbiertoDocumentoAsync(int tipo, int entry)
        {
            CabeceraOrigen? cabecera;
            if (tipo == TipoDocumentoVenta.Pedido)
            {
                cabecera = (await (await _repoPedido.ObtenerTodoAsync())
                        .Where(p => p.Entry == entry)
                        .Select(p => new { p.CodigoSn, p.TotalDoc })
                        .ToListAsync())
                    .Select(p => new CabeceraOrigen(tipo, entry, p.CodigoSn, p.TotalDoc))
                    .FirstOrDefault();
            }
            else if (tipo == TipoDocumentoVenta.Entrega)
            {
                cabecera = (await (await _repoEntrega.ObtenerTodoAsync())
                        .Where(e => e.Entry == entry)
                        .Select(e => new { e.CodigoSn, e.TotalDoc })
                        .ToListAsync())
                    .Select(e => new CabeceraOrigen(tipo, entry, e.CodigoSn, e.TotalDoc))
                    .FirstOrDefault();
            }
            else
                return 0m;

            if (cabecera is null)
                return 0m;
            var valores = await CargarOrigenesAsync(new[] { cabecera });
            return valores.TryGetValue((tipo, entry), out var origen) ? origen.Valor : 0m;
        }

        public async Task<decimal> CalcularMontoAsync(int tipoDestino, decimal totalDoc, decimal pagoInmediato, IReadOnlyList<LineaBaseRequest> lineasBase)
        {
            var liberado = 0m;
            var conBase = (lineasBase ?? Array.Empty<LineaBaseRequest>())
                .Where(l => l.BaseTipo == TipoDocumentoVenta.Pedido || l.BaseTipo == TipoDocumentoVenta.Entrega)
                .ToList();
            if (conBase.Count > 0)
            {
                // Cabeceras de los orígenes (sin importar su estado: el valor unitario no depende de él), una consulta por tabla.
                var origenes = new List<CabeceraOrigen>();
                var pedidoIds = conBase.Where(l => l.BaseTipo == TipoDocumentoVenta.Pedido).Select(l => l.BaseEntry).Distinct().ToList();
                var entregaIds = conBase.Where(l => l.BaseTipo == TipoDocumentoVenta.Entrega).Select(l => l.BaseEntry).Distinct().ToList();
                if (pedidoIds.Count > 0)
                {
                    origenes.AddRange((await (await _repoPedido.ObtenerTodoAsync())
                            .Where(p => pedidoIds.Contains(p.Entry))
                            .Select(p => new { p.Entry, p.CodigoSn, p.TotalDoc })
                            .ToListAsync())
                        .Select(p => new CabeceraOrigen(TipoDocumentoVenta.Pedido, p.Entry, p.CodigoSn, p.TotalDoc)));
                }
                if (entregaIds.Count > 0)
                {
                    origenes.AddRange((await (await _repoEntrega.ObtenerTodoAsync())
                            .Where(e => entregaIds.Contains(e.Entry))
                            .Select(e => new { e.Entry, e.CodigoSn, e.TotalDoc })
                            .ToListAsync())
                        .Select(e => new CabeceraOrigen(TipoDocumentoVenta.Entrega, e.Entry, e.CodigoSn, e.TotalDoc)));
                }

                var valores = await CargarOrigenesAsync(origenes);
                foreach (var l in conBase)
                {
                    if (!valores.TryGetValue((l.BaseTipo, l.BaseEntry), out var origen))
                        continue;
                    var lineaOrigen = origen.Lineas.FirstOrDefault(x => x.NoLinea == l.BaseLinea);
                    if (lineaOrigen is null)
                        continue;
                    liberado += l.Cantidad * lineaOrigen.ValorUnitario;
                }
            }
            return Math.Max(0m, Redondear(totalDoc - pagoInmediato - liberado));
        }

        public async Task<ResultadoCredito> VerificarAsync(string? codigoSn, decimal monto)
        {
            // Sin monto nuevo no hay nada que verificar (ni siquiera se consultan saldos).
            if (monto == 0m || string.IsNullOrEmpty(codigoSn))
                return new ResultadoCredito(true, null, null, monto, 0m);

            // Socio inexistente o que no es cliente: no hay posición, no se controla.
            var posicion = await ObtenerPosicionAsync(codigoSn);
            if (posicion is null)
                return new ResultadoCredito(true, null, null, monto, 0m);

            if (posicion.BloquearSiVencido && posicion.Vencido > 0m)
                return new ResultadoCredito(false, "VENCIDO", posicion, monto, monto);

            if (posicion.Limite.HasValue)
            {
                var exceso = posicion.Exposicion + monto - posicion.Limite.Value;
                if (exceso > 0m)
                    return new ResultadoCredito(false, "LIMITE", posicion, monto, exceso);
            }
            return new ResultadoCredito(true, null, posicion, monto, 0m);
        }

        private static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
    }
}
