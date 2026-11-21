using API.Domain.Entity.Models;
using API.Domain.Interface.Existencias;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Domain.Core.Existencias
{
    /// <summary>
    /// Mantiene <c>ExistenciaArticulo.Comprometido</c> (pedidos de venta abiertos) y <c>Pedido</c> (pedidos de compra abiertos)
    /// recalculándolos desde los documentos. Ver spec 2026-11-20-comprometido-y-pedido-design.md.
    /// No toca <c>Disponible</c> ni genera movimientos de inventario.
    /// </summary>
    public class ExistenciaCompromisoService : IExistenciaCompromisoService
    {
        // Tamaño máximo de las listas que se envían a SQL con Contains (SQL Server limita las listas IN).
        private const int Lote = 1000;

        // Escala de las columnas Comprometido/Pedido (decimal(19, 6)): se redondea a ella para no ver diferencias eternas.
        private const int Escala = 6;

        private readonly IRepositorioGenerico<Pedido, int> _pedido;
        private readonly IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)> _pedidoDet;
        private readonly IRepositorioGenerico<Entrega, int> _entrega;
        private readonly IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)> _entregaDet;
        private readonly IRepositorioGenerico<Factura, int> _factura;
        private readonly IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> _facturaDet;
        private readonly IRepositorioGenerico<PedidoCompra, int> _pedidoCompra;
        private readonly IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)> _pedidoCompraDet;
        private readonly IRepositorioGenerico<EntregaCompra, int> _entregaCompra;
        private readonly IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)> _entregaCompraDet;
        private readonly IRepositorioGenerico<FacturaCompra, int> _facturaCompra;
        private readonly IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> _facturaCompraDet;
        private readonly IRepositorioGenerico<Articulo, string> _articulo;
        private readonly IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)> _existencia;
        private readonly ILogger<ExistenciaCompromisoService> _logger;

        public ExistenciaCompromisoService(
            IRepositorioGenerico<Pedido, int> pedido,
            IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)> pedidoDet,
            IRepositorioGenerico<Entrega, int> entrega,
            IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)> entregaDet,
            IRepositorioGenerico<Factura, int> factura,
            IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> facturaDet,
            IRepositorioGenerico<PedidoCompra, int> pedidoCompra,
            IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)> pedidoCompraDet,
            IRepositorioGenerico<EntregaCompra, int> entregaCompra,
            IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)> entregaCompraDet,
            IRepositorioGenerico<FacturaCompra, int> facturaCompra,
            IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> facturaCompraDet,
            IRepositorioGenerico<Articulo, string> articulo,
            IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)> existencia,
            ILogger<ExistenciaCompromisoService> logger)
        {
            _pedido = pedido;
            _pedidoDet = pedidoDet;
            _entrega = entrega;
            _entregaDet = entregaDet;
            _factura = factura;
            _facturaDet = facturaDet;
            _pedidoCompra = pedidoCompra;
            _pedidoCompraDet = pedidoCompraDet;
            _entregaCompra = entregaCompra;
            _entregaCompraDet = entregaCompraDet;
            _facturaCompra = facturaCompra;
            _facturaCompraDet = facturaCompraDet;
            _articulo = articulo;
            _existencia = existencia;
            _logger = logger;
        }

        // Filas ya proyectadas (solo las columnas necesarias).
        private sealed record LineaDoc(string? CodArticulo, string? CodAlmacen, int? BaseTipo, int? BaseEntry, int? BaseLinea);
        private sealed record LineaPedidoRef(int Entry, int NoLinea, string? CodArticulo, string? CodAlmacen);

        // ---------------------------------------------------------------------------------------------
        // API pública
        // ---------------------------------------------------------------------------------------------

        public async Task RecalcularAsync(IReadOnlyCollection<ParExistencia> pares)
        {
            var lista = Normalizar(pares);
            if (lista.Count == 0) return;

            var ventas = await SumasVentasAsync(lista);
            var compras = await SumasComprasAsync(lista);

            foreach (var par in lista)
            {
                var comprometido = Redondear(ventas.TryGetValue(par, out var v) ? v : 0m);
                var pedido = Redondear(compras.TryGetValue(par, out var c) ? c : 0m);
                var existente = await _existencia.ObtenerAsync((par.CodArticulo, par.CodAlmacen));
                await AplicarAsync(par, existente, comprometido, pedido);
            }
        }

        public async Task RecalcularPorDocumentoAsync(int tipoDoc, int entry)
        {
            // Entrega/factura sin base de pedido (p. ej. ventas POS): no consume nada, no hay nada que recalcular.
            var pares = await ParesDeDocumentoAsync(tipoDoc, entry, soloSiTienePedidoBase: true);
            if (pares.Count == 0) return;
            await RecalcularAsync(pares);
        }

        public async Task<ResultadoRecalculo> RecalcularTodoAsync()
        {
            var ventas = await SumasVentasAsync(null);
            var compras = await SumasComprasAsync(null);

            // Filas de existencias con algún valor (una sola consulta, solo las columnas necesarias).
            var conValor = await (await _existencia.ObtenerTodoAsync())
                .Where(e => e.Comprometido != 0m || e.Pedido != 0m)
                .Select(e => new { e.CodArticulo, e.CodAlmacen, e.Comprometido, e.Pedido })
                .ToListAsync();

            var actuales = new Dictionary<ParExistencia, (decimal Comprometido, decimal Pedido)>(CompromisoCalculadora.Comparador);
            foreach (var f in conValor)
            {
                var cod = (f.CodArticulo ?? string.Empty).Trim();
                var alm = (f.CodAlmacen ?? string.Empty).Trim();
                if (cod.Length == 0 || alm.Length == 0) continue;
                actuales[new ParExistencia(cod, alm)] = (f.Comprometido, f.Pedido);
            }

            var pares = new Dictionary<ParExistencia, byte>(CompromisoCalculadora.Comparador);
            foreach (var p in actuales.Keys) pares[p] = 0;
            foreach (var p in ventas.Keys) pares[p] = 0;
            foreach (var p in compras.Keys) pares[p] = 0;

            var filas = 0;
            var corregidas = 0;
            foreach (var par in pares.Keys)
            {
                filas++;
                var comprometido = Redondear(ventas.TryGetValue(par, out var v) ? v : 0m);
                var pedido = Redondear(compras.TryGetValue(par, out var c) ? c : 0m);

                if (actuales.TryGetValue(par, out var actual) && actual.Comprometido == comprometido && actual.Pedido == pedido)
                    continue; // sin cambio: no se vuelve a leer ni se escribe

                var existente = await _existencia.ObtenerAsync((par.CodArticulo, par.CodAlmacen));
                if (await AplicarAsync(par, existente, comprometido, pedido))
                    corregidas++;
            }
            return new ResultadoRecalculo(filas, corregidas);
        }

        public async Task<IReadOnlyList<ParExistencia>> ObtenerParesAsync(int tipoDoc, int entry) =>
            await ParesDeDocumentoAsync(tipoDoc, entry, soloSiTienePedidoBase: false);

        public async Task NotificarDocumentoAsync(int tipoDoc, int entry)
        {
            try
            {
                await RecalcularPorDocumentoAsync(tipoDoc, entry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo recalcular el comprometido y el pedido del documento tipo {TipoDoc} entry {Entry}. Use Recalcular todo para repararlo.", tipoDoc, entry);
            }
        }

        public async Task NotificarParesAsync(IReadOnlyCollection<ParExistencia> pares)
        {
            if (pares is null || pares.Count == 0) return;
            try
            {
                await RecalcularAsync(pares);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo recalcular el comprometido y el pedido de {Cantidad} par(es) artículo-almacén. Use Recalcular todo para repararlo.", pares.Count);
            }
        }

        // ---------------------------------------------------------------------------------------------
        // Escritura
        // ---------------------------------------------------------------------------------------------

        private static decimal Redondear(decimal valor) => decimal.Round(valor, Escala, MidpointRounding.AwayFromZero);

        /// <summary>Escribe los valores calculados si hace falta. Devuelve true si creó o cambió la fila.</summary>
        private async Task<bool> AplicarAsync(ParExistencia par, ExistenciaArticulo? existente, decimal comprometido, decimal pedido)
        {
            try
            {
                return await EscribirAsync(par, existente, comprometido, pedido);
            }
            catch
            {
                // Un guardado fallido (concurrencia, llave duplicada) deja la fila Added/Modified en el rastreador del contexto de la
                // petición y cualquier SaveChanges posterior la reintentaría y fallaría: se descarta antes de propagar el error.
                _existencia.DescartarCambiosPendientes();
                throw;
            }
        }

        private async Task<bool> EscribirAsync(ParExistencia par, ExistenciaArticulo? existente, decimal comprometido, decimal pedido)
        {
            if (existente is null)
            {
                if (comprometido == 0m && pedido == 0m) return false;
                await _existencia.InsertarAsync(new ExistenciaArticulo
                {
                    CodArticulo = par.CodArticulo,
                    CodAlmacen = par.CodAlmacen,
                    Disponible = 0m,
                    Comprometido = comprometido,
                    Pedido = pedido,
                    FechaActualizacion = DateTime.Now
                });
                return true;
            }

            if (existente.Comprometido == comprometido && existente.Pedido == pedido) return false;

            existente.Comprometido = comprometido;
            existente.Pedido = pedido;
            existente.FechaActualizacion = DateTime.Now;
            await _existencia.ActualizarAsync((existente.CodArticulo, existente.CodAlmacen), existente);
            return true;
        }

        // ---------------------------------------------------------------------------------------------
        // Pares y artículos
        // ---------------------------------------------------------------------------------------------

        /// <summary>Recorta, descarta nulos/vacíos y elimina duplicados (sin distinguir mayúsculas).</summary>
        private static List<ParExistencia> Normalizar(IEnumerable<ParExistencia?>? pares)
        {
            var resultado = new List<ParExistencia>();
            if (pares is null) return resultado;
            var vistos = new HashSet<ParExistencia>(CompromisoCalculadora.Comparador);
            foreach (var p in pares)
            {
                var cod = p?.CodArticulo?.Trim();
                var alm = p?.CodAlmacen?.Trim();
                if (string.IsNullOrEmpty(cod) || string.IsNullOrEmpty(alm)) continue;
                var par = new ParExistencia(cod, alm);
                if (vistos.Add(par)) resultado.Add(par);
            }
            return resultado;
        }

        private static ParExistencia? ParDe(string? codArticulo, string? codAlmacen)
        {
            var cod = codArticulo?.Trim();
            var alm = codAlmacen?.Trim();
            return string.IsNullOrEmpty(cod) || string.IsNullOrEmpty(alm) ? null : new ParExistencia(cod, alm);
        }

        /// <summary>Códigos (recortados) de los artículos de inventario entre los indicados. Una consulta por lote de 1,000.</summary>
        private async Task<HashSet<string>> InventariablesAsync(IEnumerable<string> codigos)
        {
            var resultado = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var distintos = codigos.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (distintos.Count == 0) return resultado;

            var articulos = await _articulo.ObtenerTodoAsync();
            foreach (var lote in distintos.Chunk(Lote))
            {
                var codigosLote = lote.ToList();
                var encontrados = await articulos
                    .Where(a => codigosLote.Contains(a.Codigo) && a.ArticuloInventario == "S")
                    .Select(a => a.Codigo)
                    .ToListAsync();
                foreach (var c in encontrados) resultado.Add(c.Trim());
            }
            return resultado;
        }

        // ---------------------------------------------------------------------------------------------
        // Sumas de lo abierto (ventas = pedidos de venta; compras = pedidos de compra). El consumo se calcula en SQL: por cada línea
        // de pedido activa de un artículo de inventario se resta el consumo (subconsulta correlacionada sobre entregas y facturas
        // activas) y solo vuelven las líneas con cantidad abierta; la suma por (artículo, almacén) la hace CompromisoCalculadora.
        // (La suma agrupada dentro de SQL no se puede traducir: SQL Server no admite un agregado sobre una subconsulta.)
        // ---------------------------------------------------------------------------------------------

        private sealed record LineaAbierta(string? CodArticulo, string? CodAlmacen, decimal Cantidad, decimal Consumido, decimal Factor);

        private Task<Dictionary<ParExistencia, decimal>> SumasVentasAsync(IReadOnlyCollection<ParExistencia>? pares) =>
            SumarAsync(pares, ConsultarAbiertoVentaAsync);

        private Task<Dictionary<ParExistencia, decimal>> SumasComprasAsync(IReadOnlyCollection<ParExistencia>? pares) =>
            SumarAsync(pares, ConsultarAbiertoCompraAsync);

        /// <summary>
        /// Con pares: consulta por lotes de artículos (y filtra por almacén) y deja solo los pares pedidos; sin pares (null): todos
        /// los que tienen líneas abiertas. Los códigos salen recortados y sin duplicados (sin distinguir mayúsculas).
        /// </summary>
        private static async Task<Dictionary<ParExistencia, decimal>> SumarAsync(
            IReadOnlyCollection<ParExistencia>? pares,
            Func<List<string>?, List<string>?, Task<List<LineaAbierta>>> consultar)
        {
            var filas = new List<LineaAbierta>();
            HashSet<ParExistencia>? solicitados = null;

            if (pares is null)
            {
                filas.AddRange(await consultar(null, null));
            }
            else
            {
                solicitados = new HashSet<ParExistencia>(pares, CompromisoCalculadora.Comparador);
                var articulos = pares.Select(p => p.CodArticulo).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var almacenes = pares.Select(p => p.CodAlmacen).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var filtroAlmacenes = almacenes.Count <= Lote ? almacenes : null; // si son demasiados, se filtra en memoria
                foreach (var lote in articulos.Chunk(Lote))
                    filas.AddRange(await consultar(lote.ToList(), filtroAlmacenes));
            }

            var abiertas = new List<LineaPedidoAbierta>();
            foreach (var f in filas)
            {
                var par = ParDe(f.CodArticulo, f.CodAlmacen);
                if (par is null) continue;
                if (solicitados is not null && !solicitados.Contains(par)) continue;
                abiertas.Add(new LineaPedidoAbierta(par.CodArticulo, par.CodAlmacen, f.Cantidad, f.Consumido, f.Factor));
            }
            return CompromisoCalculadora.SumarPorPar(abiertas);
        }

        // Pedido de venta activo = Cancelado != "S" y EstadoDoc != "C" (como DocumentoBaseService). Consumo = cantidad de líneas de
        // entregas y facturas activas (Cancelado != "S" y EstadoDoc != "C") con BaseTipo 4 que apuntan a esa línea.
        // abierto = máx(0, cantidad - consumido) × factor (factor <= 0 se trata como 1); solo artículos de inventario.
        private async Task<List<LineaAbierta>> ConsultarAbiertoVentaAsync(List<string>? articulos, List<string>? almacenes)
        {
            var detalle = (await _pedidoDet.ObtenerTodoAsync())
                .Where(d => d.CodArticulo != null && d.CodArticulo != "" && d.CodAlmacen != null && d.CodAlmacen != "");
            if (articulos is not null) detalle = detalle.Where(d => articulos.Contains(d.CodArticulo!));
            if (almacenes is not null) detalle = detalle.Where(d => almacenes.Contains(d.CodAlmacen!));
            var activos = (await _pedido.ObtenerTodoAsync()).Where(p => p.Cancelado != "S" && p.EstadoDoc != "C");
            var inventariables = (await _articulo.ObtenerTodoAsync()).Where(a => a.ArticuloInventario == "S");

            var entregas = (await _entrega.ObtenerTodoAsync()).Where(h => h.Cancelado != "S" && h.EstadoDoc != "C");
            var facturas = (await _factura.ObtenerTodoAsync()).Where(h => h.Cancelado != "S" && h.EstadoDoc != "C");
            var lineasEntrega = (await _entregaDet.ObtenerTodoAsync()).Where(c => c.BaseTipo == TipoDocCompromiso.Pedido);
            var lineasFactura = (await _facturaDet.ObtenerTodoAsync()).Where(c => c.BaseTipo == TipoDocCompromiso.Pedido);

            var lineas = from d in detalle
                         join p in activos on d.Entry equals p.Entry
                         join a in inventariables on d.CodArticulo equals a.Codigo
                         select new
                         {
                             Articulo = d.CodArticulo,
                             Almacen = d.CodAlmacen,
                             Cantidad = d.Cantidad ?? 0m,
                             Factor = d.FactorUom,
                             Consumido =
                                 (from c in lineasEntrega
                                  join h in entregas on c.Entry equals h.Entry
                                  where c.BaseEntry == d.Entry && c.BaseLinea == d.NoLinea
                                  select c.Cantidad ?? 0m).Sum()
                                 + (from c in lineasFactura
                                    join h in facturas on c.Entry equals h.Entry
                                    where c.BaseEntry == d.Entry && c.BaseLinea == d.NoLinea
                                    select c.Cantidad ?? 0m).Sum()
                         };

            // Solo vuelven las líneas con cantidad abierta: el historial totalmente consumido no se materializa.
            var filas = await lineas.Where(x => x.Cantidad > x.Consumido).ToListAsync();
            return filas.Select(f => new LineaAbierta(f.Articulo, f.Almacen, f.Cantidad, f.Consumido, f.Factor)).ToList();
        }

        // Pedido de compra activo = Cancelado != "S" y EstadoDoc != "C". Consumo = cantidad de líneas de entregas y facturas de compra
        // con encabezado no cancelado (Cancelado != "S", sin mirar EstadoDoc: como PedidoCompraDomain) y BaseTipo 11.
        private async Task<List<LineaAbierta>> ConsultarAbiertoCompraAsync(List<string>? articulos, List<string>? almacenes)
        {
            var detalle = (await _pedidoCompraDet.ObtenerTodoAsync())
                .Where(d => d.CodArticulo != null && d.CodArticulo != "" && d.CodAlmacen != null && d.CodAlmacen != "");
            if (articulos is not null) detalle = detalle.Where(d => articulos.Contains(d.CodArticulo!));
            if (almacenes is not null) detalle = detalle.Where(d => almacenes.Contains(d.CodAlmacen!));
            var activos = (await _pedidoCompra.ObtenerTodoAsync()).Where(p => p.Cancelado != "S" && p.EstadoDoc != "C");
            var inventariables = (await _articulo.ObtenerTodoAsync()).Where(a => a.ArticuloInventario == "S");

            var entregas = (await _entregaCompra.ObtenerTodoAsync()).Where(h => h.Cancelado != "S");
            var facturas = (await _facturaCompra.ObtenerTodoAsync()).Where(h => h.Cancelado != "S");
            var lineasEntrega = (await _entregaCompraDet.ObtenerTodoAsync()).Where(c => c.BaseTipo == TipoDocCompromiso.PedidoCompra);
            var lineasFactura = (await _facturaCompraDet.ObtenerTodoAsync()).Where(c => c.BaseTipo == TipoDocCompromiso.PedidoCompra);

            var lineas = from d in detalle
                         join p in activos on d.Entry equals p.Entry
                         join a in inventariables on d.CodArticulo equals a.Codigo
                         select new
                         {
                             Articulo = d.CodArticulo,
                             Almacen = d.CodAlmacen,
                             Cantidad = d.Cantidad ?? 0m,
                             Factor = d.FactorUom,
                             Consumido =
                                 (from c in lineasEntrega
                                  join h in entregas on c.Entry equals h.Entry
                                  where c.BaseEntry == d.Entry && c.BaseLinea == d.NoLinea
                                  select c.Cantidad ?? 0m).Sum()
                                 + (from c in lineasFactura
                                    join h in facturas on c.Entry equals h.Entry
                                    where c.BaseEntry == d.Entry && c.BaseLinea == d.NoLinea
                                    select c.Cantidad ?? 0m).Sum()
                         };

            // Solo vuelven las líneas con cantidad abierta: el historial totalmente consumido no se materializa.
            var filas = await lineas.Where(x => x.Cantidad > x.Consumido).ToListAsync();
            return filas.Select(f => new LineaAbierta(f.Articulo, f.Almacen, f.Cantidad, f.Consumido, f.Factor)).ToList();
        }


        // ---------------------------------------------------------------------------------------------
        // Pares de un documento
        // ---------------------------------------------------------------------------------------------

        private static bool EsCompra(int tipoDoc) => tipoDoc is TipoDocCompromiso.PedidoCompra or TipoDocCompromiso.EntregaCompra or TipoDocCompromiso.FacturaCompra;
        private static bool EsPedido(int tipoDoc) => tipoDoc is TipoDocCompromiso.Pedido or TipoDocCompromiso.PedidoCompra;

        /// <summary>
        /// Pares (artículo de inventario, almacén) de un documento: los de sus líneas y, en entregas/facturas, los de las líneas de
        /// pedido a las que apuntan. Con <paramref name="soloSiTienePedidoBase"/>, una entrega/factura sin línea que apunte a un
        /// pedido devuelve vacío sin consultar pedidos.
        /// </summary>
        private async Task<List<ParExistencia>> ParesDeDocumentoAsync(int tipoDoc, int entry, bool soloSiTienePedidoBase)
        {
            var propias = await LineasDeDocumentoAsync(tipoDoc, entry);
            if (propias.Count == 0) return new List<ParExistencia>();

            var candidatos = propias.Select(l => ParDe(l.CodArticulo, l.CodAlmacen)).ToList();

            if (!EsPedido(tipoDoc))
            {
                var tipoBase = EsCompra(tipoDoc) ? TipoDocCompromiso.PedidoCompra : TipoDocCompromiso.Pedido;
                var conBase = propias
                    .Where(l => l.BaseTipo == tipoBase && l.BaseEntry is not null && l.BaseLinea is not null)
                    .Select(l => (Entry: l.BaseEntry!.Value, NoLinea: l.BaseLinea!.Value))
                    .Distinct()
                    .ToList();

                if (conBase.Count == 0 && soloSiTienePedidoBase) return new List<ParExistencia>();

                if (conBase.Count > 0)
                {
                    var buscadas = conBase.ToHashSet();
                    var lineasPedido = await LineasDePedidosAsync(EsCompra(tipoDoc), conBase.Select(c => c.Entry).Distinct().ToList());
                    foreach (var lp in lineasPedido)
                        if (buscadas.Contains((lp.Entry, lp.NoLinea)))
                            candidatos.Add(ParDe(lp.CodArticulo, lp.CodAlmacen));
                }
            }

            var pares = Normalizar(candidatos);
            if (pares.Count == 0) return pares;

            var inventariables = await InventariablesAsync(pares.Select(p => p.CodArticulo));
            return pares.Where(p => inventariables.Contains(p.CodArticulo)).ToList();
        }

        /// <summary>Líneas propias del documento (artículo, almacén y base). Tipo desconocido o documento inexistente: vacío.</summary>
        private async Task<List<LineaDoc>> LineasDeDocumentoAsync(int tipoDoc, int entry)
        {
            switch (tipoDoc)
            {
                case TipoDocCompromiso.Pedido:
                    return (await (await _pedidoDet.ObtenerTodoAsync()).Where(d => d.Entry == entry)
                        .Select(d => new { d.CodArticulo, d.CodAlmacen, d.BaseTipo, d.BaseEntry, d.BaseLinea }).ToListAsync())
                        .Select(d => new LineaDoc(d.CodArticulo, d.CodAlmacen, d.BaseTipo, d.BaseEntry, d.BaseLinea)).ToList();
                case TipoDocCompromiso.Entrega:
                    return (await (await _entregaDet.ObtenerTodoAsync()).Where(d => d.Entry == entry)
                        .Select(d => new { d.CodArticulo, d.CodAlmacen, d.BaseTipo, d.BaseEntry, d.BaseLinea }).ToListAsync())
                        .Select(d => new LineaDoc(d.CodArticulo, d.CodAlmacen, d.BaseTipo, d.BaseEntry, d.BaseLinea)).ToList();
                case TipoDocCompromiso.Factura:
                    return (await (await _facturaDet.ObtenerTodoAsync()).Where(d => d.Entry == entry)
                        .Select(d => new { d.CodArticulo, d.CodAlmacen, d.BaseTipo, d.BaseEntry, d.BaseLinea }).ToListAsync())
                        .Select(d => new LineaDoc(d.CodArticulo, d.CodAlmacen, d.BaseTipo, d.BaseEntry, d.BaseLinea)).ToList();
                case TipoDocCompromiso.PedidoCompra:
                    return (await (await _pedidoCompraDet.ObtenerTodoAsync()).Where(d => d.Entry == entry)
                        .Select(d => new { d.CodArticulo, d.CodAlmacen, d.BaseTipo, d.BaseEntry, d.BaseLinea }).ToListAsync())
                        .Select(d => new LineaDoc(d.CodArticulo, d.CodAlmacen, d.BaseTipo, d.BaseEntry, d.BaseLinea)).ToList();
                case TipoDocCompromiso.EntregaCompra:
                    return (await (await _entregaCompraDet.ObtenerTodoAsync()).Where(d => d.Entry == entry)
                        .Select(d => new { d.CodArticulo, d.CodAlmacen, d.BaseTipo, d.BaseEntry, d.BaseLinea }).ToListAsync())
                        .Select(d => new LineaDoc(d.CodArticulo, d.CodAlmacen, d.BaseTipo, d.BaseEntry, d.BaseLinea)).ToList();
                case TipoDocCompromiso.FacturaCompra:
                    return (await (await _facturaCompraDet.ObtenerTodoAsync()).Where(d => d.Entry == entry)
                        .Select(d => new { d.CodArticulo, d.CodAlmacen, d.BaseTipo, d.BaseEntry, d.BaseLinea }).ToListAsync())
                        .Select(d => new LineaDoc(d.CodArticulo, d.CodAlmacen, d.BaseTipo, d.BaseEntry, d.BaseLinea)).ToList();
                default:
                    return new List<LineaDoc>();
            }
        }

        /// <summary>Líneas de los pedidos indicados (venta o compra), por lotes de 1,000 entries.</summary>
        private async Task<List<LineaPedidoRef>> LineasDePedidosAsync(bool compra, List<int> entries)
        {
            var resultado = new List<LineaPedidoRef>();
            foreach (var lote in entries.Chunk(Lote))
            {
                var entriesLote = lote.ToList();
                if (compra)
                {
                    var filas = await (await _pedidoCompraDet.ObtenerTodoAsync()).Where(d => entriesLote.Contains(d.Entry))
                        .Select(d => new { d.Entry, d.NoLinea, d.CodArticulo, d.CodAlmacen }).ToListAsync();
                    resultado.AddRange(filas.Select(f => new LineaPedidoRef(f.Entry, f.NoLinea, f.CodArticulo, f.CodAlmacen)));
                }
                else
                {
                    var filas = await (await _pedidoDet.ObtenerTodoAsync()).Where(d => entriesLote.Contains(d.Entry))
                        .Select(d => new { d.Entry, d.NoLinea, d.CodArticulo, d.CodAlmacen }).ToListAsync();
                    resultado.AddRange(filas.Select(f => new LineaPedidoRef(f.Entry, f.NoLinea, f.CodArticulo, f.CodAlmacen)));
                }
            }
            return resultado;
        }
    }
}
