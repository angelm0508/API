using System.Data;
using API.Domain.Core.Reportes.Compartido;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Domain.Interface.Pos;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core.Pos
{
    /// <summary>
    /// Casos de uso del POS: configuración, catálogo, venta atómica (factura + cobro en una sola transacción), ticket y
    /// ventas del día.
    /// </summary>
    public class PosDomain : IPosDomain
    {
        private const int IdConfiguracion = 1;

        private readonly IRepositorioGenerico<PosConfiguracion, int> _repoConfiguracion;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoSerie;
        private readonly IRepositorioGenerico<Almacen, string> _repoAlmacen;
        private readonly IRepositorioGenerico<Impuesto, string> _repoImpuesto;
        private readonly IRepositorioGenerico<SocioNegocio, string> _repoSocio;
        private readonly IRepositorioGenerico<FormaPago, string> _repoFormaPago;
        private readonly IRepositorioGenerico<Monedum, string> _repoMoneda;
        private readonly IRepositorioGenerico<NumeracionDocumento, string> _repoNumeracion;

        // Catálogo
        private const int LongitudMinimaBusqueda = 2;
        private const int MaximoBusqueda = 20;
        private const int MaximoClientes = 10;
        private const int TopeCandidatos = 100;
        private const int MaximoRapidosMarcados = 24;
        private const int MaximoMasVendidos = 12;
        private const int DiasMasVendidos = 30;
        private const int TamanoTanda = 50;

        private readonly IRepositorioGenerico<Articulo, string> _repoArticulo;
        private readonly IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)> _repoExistencia;
        private readonly IPrecioListaDomain _precios;
        private readonly ConsultaLineasVenta _consultaLineas;

        // Venta, ticket y ventas del día
        private const int MaximoVentasDelDia = 200;
        private const string CondicionContado = "CON";
        private const string TipoObjetoFactura = "6";

        private readonly IFacturaDomain _facturaDomain;
        private readonly IPagoRecibidoDomain _pagoDomain;
        private readonly IEjecutorTransaccion _tx;
        private readonly IRepositorioGenerico<Factura, int> _repoFactura;
        private readonly IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> _repoDetalle;
        private readonly IRepositorioGenerico<PagoRecibido, int> _repoPago;
        private readonly IRepositorioGenerico<PagoRecibidoFactura, (int Entry, int NoLinea)> _repoPagoFactura;
        private readonly IRepositorioGenerico<PagoRecibidoMedio, (int Entry, int NoLinea)> _repoMedio;
        private readonly IBloqueoSeries _bloqueoSeries;

        public PosDomain(
            IRepositorioGenerico<PosConfiguracion, int> repoConfiguracion,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoSerie,
            IRepositorioGenerico<Almacen, string> repoAlmacen,
            IRepositorioGenerico<Impuesto, string> repoImpuesto,
            IRepositorioGenerico<SocioNegocio, string> repoSocio,
            IRepositorioGenerico<FormaPago, string> repoFormaPago,
            IRepositorioGenerico<Monedum, string> repoMoneda,
            IRepositorioGenerico<Articulo, string> repoArticulo,
            IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)> repoExistencia,
            IPrecioListaDomain precios,
            ConsultaLineasVenta consultaLineas,
            IFacturaDomain facturaDomain,
            IPagoRecibidoDomain pagoDomain,
            IEjecutorTransaccion tx,
            IRepositorioGenerico<Factura, int> repoFactura,
            IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> repoDetalle,
            IRepositorioGenerico<PagoRecibido, int> repoPago,
            IRepositorioGenerico<PagoRecibidoFactura, (int Entry, int NoLinea)> repoPagoFactura,
            IRepositorioGenerico<PagoRecibidoMedio, (int Entry, int NoLinea)> repoMedio,
            IBloqueoSeries bloqueoSeries,
            IRepositorioGenerico<NumeracionDocumento, string> repoNumeracion)
        {
            _bloqueoSeries = bloqueoSeries;
            _repoNumeracion = repoNumeracion;
            _facturaDomain = facturaDomain;
            _pagoDomain = pagoDomain;
            _tx = tx;
            _repoFactura = repoFactura;
            _repoDetalle = repoDetalle;
            _repoPago = repoPago;
            _repoPagoFactura = repoPagoFactura;
            _repoMedio = repoMedio;
            _repoArticulo = repoArticulo;
            _repoExistencia = repoExistencia;
            _precios = precios;
            _consultaLineas = consultaLineas;
            _repoConfiguracion = repoConfiguracion;
            _repoSerie = repoSerie;
            _repoAlmacen = repoAlmacen;
            _repoImpuesto = repoImpuesto;
            _repoSocio = repoSocio;
            _repoFormaPago = repoFormaPago;
            _repoMoneda = repoMoneda;
        }

        // ---------- Configuración ----------

        public async Task<PosConfiguracionVista> ObtenerConfiguracionAsync()
        {
            var fila = await _repoConfiguracion.ObtenerAsync(IdConfiguracion);
            var dato = new PosConfiguracionDato(
                fila?.SerieFactura, fila?.SeriePago, Texto(fila?.CodAlmacen), Texto(fila?.CodigoImpuesto),
                Texto(fila?.ClienteDefecto), Texto(fila?.FormaPagoEfectivo), Texto(fila?.MonedaDoc));

            var faltantes = new List<string>();
            if (dato.SerieFactura is null) faltantes.Add("Serie de factura POS");
            if (dato.SeriePago is null) faltantes.Add("Serie de pagos");
            if (dato.CodAlmacen is null) faltantes.Add("Almacén POS");
            if (dato.CodigoImpuesto is null) faltantes.Add("Impuesto por defecto");
            if (dato.ClienteDefecto is null) faltantes.Add("Cliente por defecto");
            if (dato.FormaPagoEfectivo is null) faltantes.Add("Forma de pago en efectivo");
            if (dato.MonedaDoc is null) faltantes.Add("Moneda");

            return new PosConfiguracionVista(dato, faltantes);
        }

        public async Task GuardarConfiguracionAsync(PosConfiguracionDato dato)
        {
            var almacen = Texto(dato.CodAlmacen);
            var impuesto = Texto(dato.CodigoImpuesto);
            var cliente = Texto(dato.ClienteDefecto);
            var formaPago = Texto(dato.FormaPagoEfectivo);
            var moneda = Texto(dato.MonedaDoc);

            // Se valida todo antes de guardar: un rechazo no deja la configuración a medias.
            if (dato.SerieFactura is int serieFactura && !await SerieValidaAsync(serieFactura, "6", exigeSubTipoBase: true))
                throw new InvalidOperationException("La serie de factura POS no es válida: debe ser una serie automática y activa de facturas.");

            // FacturaDomain y PagoRecibidoDomain numeran fuera de su transacción y sin candado: la serie por defecto de su
            // documento (la que usan las facturas y pagos normales) no puede ser también la del POS.
            if (dato.SerieFactura is int serieFacturaDefecto && await EsSeriePorDefectoAsync(serieFacturaDefecto, "6"))
                throw new InvalidOperationException("La serie de factura POS es la serie por defecto de facturas: usa una serie exclusiva para el POS.");

            if (dato.SeriePago is int seriePago && !await SerieValidaAsync(seriePago, "24", exigeSubTipoBase: false))
                throw new InvalidOperationException("La serie de pagos no es válida: debe ser una serie automática y activa de pagos recibidos.");

            if (dato.SeriePago is int seriePagoDefecto && await EsSeriePorDefectoAsync(seriePagoDefecto, "24"))
                throw new InvalidOperationException("La serie de pagos es la serie por defecto de pagos recibidos: usa una serie exclusiva para el POS.");

            if (almacen is not null && await _repoAlmacen.ObtenerAsync(almacen) is null)
                throw new InvalidOperationException("El almacén no existe.");

            if (impuesto is not null && await _repoImpuesto.ObtenerAsync(impuesto) is null)
                throw new InvalidOperationException("El impuesto no existe.");

            if (cliente is not null && (await _repoSocio.ObtenerAsync(cliente))?.TipoSn != "C")
                throw new InvalidOperationException("El cliente por defecto no existe o no es un cliente.");

            if (formaPago is not null && (await _repoFormaPago.ObtenerAsync(formaPago))?.Activo != "S")
                throw new InvalidOperationException("La forma de pago en efectivo no existe o está inactiva.");

            if (moneda is not null && await _repoMoneda.ObtenerAsync(moneda) is null)
                throw new InvalidOperationException("La moneda no existe.");

            var entidad = new PosConfiguracion
            {
                Id = IdConfiguracion,
                SerieFactura = dato.SerieFactura,
                SeriePago = dato.SeriePago,
                CodAlmacen = almacen,
                CodigoImpuesto = impuesto,
                ClienteDefecto = cliente,
                FormaPagoEfectivo = formaPago,
                MonedaDoc = moneda
            };

            if (await _repoConfiguracion.ObtenerAsync(IdConfiguracion) is null)
                await _repoConfiguracion.InsertarAsync(entidad);
            else
                await _repoConfiguracion.ActualizarAsync(IdConfiguracion, entidad);
        }

        /// <summary>Existe, es del documento pedido, automática (no manual) y no bloqueada; las de factura además con subtipo «--».</summary>
        private async Task<bool> SerieValidaAsync(int serie, string codigoObj, bool exigeSubTipoBase)
        {
            var s = await _repoSerie.ObtenerAsync(serie);
            return s is not null
                && s.CodigoObj == codigoObj
                && (!exigeSubTipoBase || s.SubTipoDoc == "--")
                && s.Manual != "S"
                && s.Bloqueado != "S";
        }

        /// <summary>La serie es la por defecto (<c>SerieDfct</c>) de algún encabezado de numeración del documento indicado.</summary>
        private async Task<bool> EsSeriePorDefectoAsync(int serie, string codigoObj) =>
            await (await _repoNumeracion.ObtenerTodoAsync()).AnyAsync(n => n.CodigoObj == codigoObj && n.SerieDfct == serie);

        private static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

        // ---------- Contexto de la pantalla y clientes (todo bajo Pos.Ver) ----------

        /// <summary>
        /// Lo que la pantalla del POS necesita al cargar, sin pedir permisos de otros módulos: configuración y faltantes,
        /// formas de pago activas (por código), tasa del impuesto configurado (0 si no hay o no tiene) y nombre y NIT del
        /// cliente por defecto (nulos si no hay o no existe). Con la configuración incompleta no lanza: devuelve los faltantes.
        /// </summary>
        public async Task<PosContexto> ObtenerContextoAsync()
        {
            var vista = await ObtenerConfiguracionAsync();
            var config = vista.Configuracion;

            var formas = await (await _repoFormaPago.ObtenerTodoAsync())
                .Where(f => f.Activo == "S")
                .OrderBy(f => f.Codigo)
                .Select(f => new FormaPagoPos(f.Codigo, f.Nombre, f.TipoDato))
                .ToListAsync();

            var tasa = config.CodigoImpuesto is null ? 0m : (await _repoImpuesto.ObtenerAsync(config.CodigoImpuesto))?.Tasa ?? 0m;

            string? nombre = null, nit = null;
            if (config.ClienteDefecto is not null && await _repoSocio.ObtenerAsync(config.ClienteDefecto) is { } cliente)
            {
                nombre = string.IsNullOrWhiteSpace(cliente.Nombre) ? cliente.Codigo : cliente.Nombre;
                nit = Texto(cliente.Nit);
            }

            return new PosContexto(vista, formas, tasa, nombre, nit);
        }

        /// <summary>
        /// Hasta <see cref="MaximoClientes"/> clientes (tipo C, no inactivos) cuyo código o nombre contengan el texto (mínimo
        /// 2 caracteres; la intercalación de SQL Server no distingue mayúsculas). Filtra, ordena (nombre, código) y corta en SQL.
        /// </summary>
        public async Task<IReadOnlyList<ClientePos>> BuscarClientesAsync(string? texto)
        {
            var t = Texto(texto);
            if (t is null || t.Length < LongitudMinimaBusqueda)
                return Array.Empty<ClientePos>();

            var filas = await (await _repoSocio.ObtenerTodoAsync())
                .Where(s => s.TipoSn == "C" && s.Activo != "N"
                    && (s.Codigo.Contains(t) || (s.Nombre != null && s.Nombre.Contains(t))))
                .OrderBy(s => s.Nombre).ThenBy(s => s.Codigo)
                .Take(MaximoClientes)
                .Select(s => new { s.Codigo, s.Nombre, s.Nit, s.NumLstPrecio })
                .ToListAsync();

            return filas
                .Select(s => new ClientePos(s.Codigo, string.IsNullOrWhiteSpace(s.Nombre) ? s.Codigo : s.Nombre, Texto(s.Nit), s.NumLstPrecio))
                .ToList();
        }

        // ---------- Catálogo ----------

        /// <summary>
        /// Busca por código, código de barras o nombre (contiene, sin distinguir mayúsculas). La consulta filtra y ordena en SQL
        /// (exactos de código o barras primero, luego nombre) y trae a lo sumo <see cref="TopeCandidatos"/> filas; el precio se
        /// resuelve en ese orden y se descarta lo que no se puede vender hasta completar <see cref="MaximoBusqueda"/>.
        /// </summary>
        public async Task<IReadOnlyList<ArticuloPos>> BuscarArticulosAsync(string? texto, string? codigoCliente)
        {
            var t = Texto(texto)?.ToLowerInvariant();
            if (t is null || t.Length < LongitudMinimaBusqueda)
                return Array.Empty<ArticuloPos>();

            var config = await _repoConfiguracion.ObtenerAsync(IdConfiguracion);
            var lista = await ListaPreciosAsync(codigoCliente, config);

            var candidatos = await (await _repoArticulo.ObtenerTodoAsync())
                .Where(a => a.Activo == "S" && a.ArticuloVenta == "S"
                    && (a.Codigo.ToLower().Contains(t)
                        || (a.CodigoBarras != null && a.CodigoBarras.ToLower().Contains(t))
                        || (a.Nombre != null && a.Nombre.ToLower().Contains(t))))
                .OrderBy(a => a.Codigo.ToLower() == t || (a.CodigoBarras != null && a.CodigoBarras.ToLower() == t) ? 0 : 1)
                .ThenBy(a => a.Nombre)
                .ThenBy(a => a.Codigo)
                .Take(TopeCandidatos)
                .ToListAsync();

            var vendibles = await SeleccionarVendiblesAsync(candidatos, lista, MaximoBusqueda);
            return await ArmarAsync(vendibles, config?.CodAlmacen);
        }

        /// <summary>Coincidencia exacta por código de barras y, si no hay, por código. Null si no existe o no se puede vender.</summary>
        public async Task<ArticuloPos?> ObtenerArticuloPorCodigoAsync(string codigo, string? codigoCliente)
        {
            var c = Texto(codigo)?.ToLowerInvariant();
            if (c is null)
                return null;

            var articulos = await _repoArticulo.ObtenerTodoAsync();
            // El código escaneado identifica un artículo: primero por barras y, si no hay, por código; después se valida si es vendible.
            var articulo = await articulos.Where(a => a.CodigoBarras != null && a.CodigoBarras.ToLower() == c).OrderBy(a => a.Codigo).FirstOrDefaultAsync()
                ?? await articulos.Where(a => a.Codigo.ToLower() == c).FirstOrDefaultAsync();
            if (articulo is null || articulo.Activo != "S" || articulo.ArticuloVenta != "S")
                return null;

            var config = await _repoConfiguracion.ObtenerAsync(IdConfiguracion);
            var lista = await ListaPreciosAsync(codigoCliente, config);
            var vendibles = await SeleccionarVendiblesAsync(new[] { articulo }, lista, 1);
            return (await ArmarAsync(vendibles, config?.CodAlmacen)).FirstOrDefault();
        }

        /// <summary>Los marcados «Mostrar en POS» (máx. 24, por nombre) o, si no hay ninguno vendible, los 12 más vendidos de 30 días.</summary>
        public async Task<IReadOnlyList<ArticuloPos>> ObtenerRapidosAsync()
        {
            var config = await _repoConfiguracion.ObtenerAsync(IdConfiguracion);
            var lista = await ListaPreciosAsync(null, config);

            var marcados = await (await _repoArticulo.ObtenerTodoAsync())
                .Where(a => a.Activo == "S" && a.ArticuloVenta == "S" && a.MostrarEnPos == "S")
                .OrderBy(a => a.Nombre).ThenBy(a => a.Codigo)
                .Take(TopeCandidatos)
                .ToListAsync();

            var vendibles = await SeleccionarVendiblesAsync(marcados, lista, MaximoRapidosMarcados);
            if (vendibles.Count == 0)
                vendibles = await MasVendidosAsync(lista);

            return await ArmarAsync(vendibles, config?.CodAlmacen);
        }

        private async Task<List<(Articulo Articulo, decimal Precio)>> MasVendidosAsync(int? lista)
        {
            var hoy = DateTime.Today;
            IReadOnlyList<LineaVenta> lineas;
            try
            {
                lineas = await _consultaLineas.ObtenerAsync(new FiltroLineasVenta(hoy.AddDays(-DiasMasVendidos), hoy.AddDays(1), null, null, null, null));
            }
            catch (ReporteInvalidoException)
            {
                // Demasiadas líneas en el período: el mensaje del reporte («Acorta el rango de fechas») no le sirve al
                // cajero; el respaldo es solo una comodidad, así que simplemente no hay rápidos.
                return new List<(Articulo Articulo, decimal Precio)>();
            }

            var ordenados = lineas
                .GroupBy(l => l.CodArticulo)
                .Select(g => (Codigo: g.Key, Cantidad: g.Sum(l => l.CantidadBase)))
                .Where(x => x.Cantidad > 0m)
                .OrderByDescending(x => x.Cantidad).ThenBy(x => x.Codigo, StringComparer.Ordinal)
                .Select(x => x.Codigo)
                .ToList();

            var resultado = new List<(Articulo, decimal)>();
            var articulos = await _repoArticulo.ObtenerTodoAsync();
            // Por tandas pequeñas de códigos en orden de ventas: no se leen todos los artículos vendidos si las primeras tandas bastan.
            foreach (var tanda in ordenados.Chunk(TamanoTanda))
            {
                var codigos = tanda.ToList();
                var filas = await articulos
                    .Where(a => a.Activo == "S" && a.ArticuloVenta == "S" && codigos.Contains(a.Codigo))
                    .ToListAsync();
                var porCodigo = filas.ToDictionary(a => a.Codigo, StringComparer.Ordinal);
                var enOrden = codigos.Where(porCodigo.ContainsKey).Select(c => porCodigo[c]).ToList();

                resultado.AddRange(await SeleccionarVendiblesAsync(enOrden, lista, MaximoMasVendidos - resultado.Count));
                if (resultado.Count >= MaximoMasVendidos)
                    break;
            }
            return resultado;
        }

        /// <summary>Lista de precios del cliente indicado o, si no hay, del cliente por defecto; nula = precio base.</summary>
        private async Task<int?> ListaPreciosAsync(string? codigoCliente, PosConfiguracion? config)
        {
            var cliente = Texto(codigoCliente) ?? Texto(config?.ClienteDefecto);
            if (cliente is null)
                return null;
            return (await _repoSocio.ObtenerAsync(cliente))?.NumLstPrecio;
        }

        /// <summary>Resuelve el precio en el orden recibido y conserva los que valen más de cero, hasta <paramref name="maximo"/>.</summary>
        private async Task<List<(Articulo Articulo, decimal Precio)>> SeleccionarVendiblesAsync(
            IEnumerable<Articulo> candidatos, int? lista, int maximo)
        {
            var resultado = new List<(Articulo, decimal)>();
            foreach (var articulo in candidatos)
            {
                if (resultado.Count >= maximo)
                    break;
                var precio = (await _precios.ResolverAsync(articulo.Codigo, lista)).PrecioLista;
                if (precio > 0m)
                    resultado.Add((articulo, precio));
            }
            return resultado;
        }

        /// <summary>Agrega las existencias del almacén POS (0 sin fila o sin almacén) a los artículos ya elegidos.</summary>
        private async Task<IReadOnlyList<ArticuloPos>> ArmarAsync(IReadOnlyList<(Articulo Articulo, decimal Precio)> elegidos, string? almacen)
        {
            if (elegidos.Count == 0)
                return Array.Empty<ArticuloPos>();

            var disponibles = new Dictionary<string, decimal>(StringComparer.Ordinal);
            var almacenPos = Texto(almacen);
            if (almacenPos is not null)
            {
                var codigos = elegidos.Select(e => e.Articulo.Codigo).ToList();   // máx. 24 códigos
                var filas = await (await _repoExistencia.ObtenerTodoAsync())
                    .Where(e => e.CodAlmacen == almacenPos && codigos.Contains(e.CodArticulo))
                    .Select(e => new { e.CodArticulo, e.Disponible })
                    .ToListAsync();
                foreach (var f in filas)
                    disponibles[f.CodArticulo] = f.Disponible;
            }

            return elegidos
                .Select(e => new ArticuloPos(
                    e.Articulo.Codigo, e.Articulo.CodigoBarras, e.Articulo.Nombre ?? e.Articulo.Codigo, e.Precio,
                    disponibles.TryGetValue(e.Articulo.Codigo, out var d) ? d : 0m,
                    e.Articulo.ArticuloInventario == "S"))
                .ToList();
        }

        // ---------- Venta ----------

        /// <summary>
        /// Registra la venta: valida todo antes de numerar y luego crea la factura de contado y su cobro dentro de UNA
        /// transacción exterior (Serializable: el pago la pide para su chequeo de saldo y, al anidarse, rige la del
        /// exterior). Las excepciones de las operaciones anidadas se dejan propagar: el exterior revierte todo.
        /// </summary>
        public async Task<TicketPos> VenderAsync(VentaPosSolicitud solicitud, string cajero)
        {
            // 1. Configuración completa.
            var vista = await ObtenerConfiguracionAsync();
            if (vista.Faltantes.Count > 0)
                throw new InvalidOperationException($"El POS no está configurado: falta {string.Join(", ", vista.Faltantes)}.");
            var config = vista.Configuracion;

            // 2. Cliente: el indicado o el de la configuración; existe, es cliente y no está inactivo.
            var codigoCliente = Texto(solicitud.CodigoCliente) ?? config.ClienteDefecto!;
            var socio = await _repoSocio.ObtenerAsync(codigoCliente);
            if (socio is null || socio.TipoSn != "C" || socio.Activo == "N")
                throw new InvalidOperationException("El cliente no existe o no es un cliente.");

            // 3. Líneas: al menos una, cantidad > 0, repetidos sumados (orden de primera aparición).
            var solicitadas = solicitud.Lineas ?? new List<VentaPosLinea>();
            if (solicitadas.Count == 0)
                throw new InvalidOperationException("Agrega al menos un artículo a la venta.");

            var agrupadas = new List<(string Codigo, decimal Cantidad)>();
            var indice = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var linea in solicitadas)
            {
                var codigo = (linea.CodArticulo ?? string.Empty).Trim();
                if (linea.Cantidad <= 0m)
                    throw new InvalidOperationException($"La cantidad de {codigo} debe ser mayor que cero.");
                if (indice.TryGetValue(codigo, out var i))
                    agrupadas[i] = (agrupadas[i].Codigo, agrupadas[i].Cantidad + linea.Cantidad);
                else
                {
                    indice[codigo] = agrupadas.Count;
                    agrupadas.Add((codigo, linea.Cantidad));
                }
            }

            // 4. Artículos vendibles y precio de la lista del cliente (se ignora cualquier precio del cliente).
            var tasa = (await _repoImpuesto.ObtenerAsync(config.CodigoImpuesto!))?.Tasa ?? 0m;
            var calculadas = new List<LineaPosCalculada>();
            var articulos = new List<Articulo>();
            foreach (var (codigo, cantidad) in agrupadas)
            {
                var articulo = codigo.Length == 0 ? null : await _repoArticulo.ObtenerAsync(codigo);
                var precio = articulo is null || articulo.Activo != "S" || articulo.ArticuloVenta != "S"
                    ? 0m
                    : (await _precios.ResolverAsync(articulo.Codigo, socio.NumLstPrecio)).PrecioLista;
                if (precio <= 0m)
                    throw new InvalidOperationException($"El artículo {codigo} no se puede vender en el POS.");

                articulos.Add(articulo!);
                calculadas.Add(PosCalculadora.Linea(articulo!.Codigo, cantidad, precio, tasa));
            }
            var totales = PosCalculadora.Totales(calculadas);
            if (totales.Total <= 0m)
                throw new InvalidOperationException("El total de la venta debe ser mayor que cero.");

            // 5. Pagos: códigos normalizados contra el catálogo; luego total, cambio y efectivo.
            var formas = await (await _repoFormaPago.ObtenerTodoAsync()).ToListAsync();
            var porCodigo = formas
                .GroupBy(f => f.Codigo.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var pagos = new List<VentaPosPago>();
            foreach (var pago in solicitud.Pagos ?? new List<VentaPosPago>())
            {
                var codigo = pago.CodFormaPago?.Trim();
                if (string.IsNullOrEmpty(codigo))
                {
                    pagos.Add(pago);   // la calculadora responde «La forma de pago es requerida.»
                    continue;
                }
                if (!porCodigo.TryGetValue(codigo, out var forma) || forma.Activo != "S")
                    throw new InvalidOperationException($"La forma de pago {codigo} no existe o está inactiva.");
                pagos.Add(pago with { CodFormaPago = forma.Codigo });
            }
            var efectivo = porCodigo.TryGetValue(config.FormaPagoEfectivo!, out var formaEfectivo) ? formaEfectivo.Codigo : config.FormaPagoEfectivo;
            var cobro = PosCalculadora.ValidarPagos(totales.Total, pagos, efectivo);

            var medios = cobro.Medios
                .Select(m => new PagoRecibidoMedio { CodFormaPago = m.CodFormaPago, Monto = m.Monto, Referencia = m.Referencia, Banco = m.Banco, FechaCheque = m.FechaCheque })
                .ToList();
            // Mismas reglas que aplicará el pago (datos de cheque, transferencia, tarjeta), pero antes de numerar.
            await ValidadorMediosPago.ValidarAsync(medios, c => Task.FromResult<FormaPago?>(porCodigo.TryGetValue(c, out var f) ? f : null));

            // 6. NIT: el enviado, el del cliente o CF.
            var nit = NitDocumento.Normalizar(solicitud.Nit) ?? NitDocumento.Normalizar(socio.Nit) ?? "CF";

            // 7. Factura y cobro en una sola transacción exterior.
            var hoy = DateTime.Today;
            var comentario = PosMarca.Comentario(cajero);
            var factura = new Factura
            {
                Serie = config.SerieFactura!.Value,
                NumManual = "N",
                Imprimido = "N",
                EstadoDoc = "A",
                TipoObjeto = TipoObjetoFactura,
                FechaDoc = hoy,
                FechaEmision = DateTime.Now,
                CodigoSn = socio.Codigo,
                NombreSn = socio.Nombre,
                Direccion = null,
                Nit = nit,
                MonedaDoc = config.MonedaDoc,
                CondicionPago = CondicionContado,
                Comentario = comentario,
                PrctjeImpuesto = tasa,
                PrctjeDesc = 0m,
                TotalBruto = totales.Bruto,
                TotalDesc = 0m,
                TotalImp = totales.Impuesto,
                TotalDoc = totales.Total,
                PagoInmediato = totales.Total
            };
            var lineas = calculadas
                .Select((l, i) => new FacturaDetalle
                {
                    CodArticulo = l.CodArticulo,
                    Descripcion = articulos[i].Nombre ?? l.CodArticulo,
                    Cantidad = l.Cantidad,
                    Precio = l.PrecioNeto,
                    PrecioBruto = l.PrecioNeto,
                    PrctjeDesc = 0m,
                    CodigoImpuesto = config.CodigoImpuesto,
                    Impuesto = l.Impuesto,
                    TotalLinea = l.TotalLinea,
                    CodAlmacen = config.CodAlmacen,
                    TipoObjeto = TipoObjetoFactura
                })
                .ToList();

            // Sin try/catch dentro de la operación: un fallo anidado debe llegar al ejecutor exterior para que revierta todo.
            // El try/catch envuelve al ejecutor, ya revertido: si aun así hubiera un interbloqueo (SQL Server elige una
            // víctima, error 1205), el cajero debe recibir un mensaje claro en vez del técnico de EF.
            int facturaEntry;
            try
            {
                facturaEntry = await _tx.EjecutarAsync(async () =>
                {
                    // Candado de actualización sobre las dos series, siempre en el mismo orden (factura, luego pagos) y
                    // antes de leerlas para numerar: las cajas simultáneas se ponen en fila en vez de interbloquearse
                    // (bajo SERIALIZABLE, leer la serie con candado compartido y luego actualizarla las interbloqueaba).
                    await _bloqueoSeries.BloquearAsync(config.SerieFactura!.Value);
                    await _bloqueoSeries.BloquearAsync(config.SeriePago!.Value);

                    var entry = await _facturaDomain.InsertarAsync(factura, lineas);

                    var pago = new PagoRecibido
                    {
                        Serie = config.SeriePago!.Value,
                        CodigoSn = socio.Codigo,
                        NombreSn = socio.Nombre,
                        FechaDoc = hoy,
                        Comentario = comentario
                    };
                    var aplicadas = new List<PagoRecibidoFactura> { new() { FacturaEntry = entry, MontoAplicado = totales.Total } };
                    await _pagoDomain.InsertarAsync(pago, aplicadas, medios);

                    return entry;
                }, IsolationLevel.Serializable);
            }
            catch (Exception ex) when (ErrorSqlTransitorio.EsInterbloqueo(ex))
            {
                throw new Exception("Otra caja está registrando una venta en este momento; intenta cobrar de nuevo.", ex);
            }

            var nombreSerie = (await _repoSerie.ObtenerAsync(factura.Serie))?.NombreSerie ?? string.Empty;
            return new TicketPos(
                facturaEntry, factura.Serie, nombreSerie, factura.NumDoc, factura.FechaEmision ?? hoy, cajero,
                socio.Codigo, socio.Nombre ?? socio.Codigo, nit,
                calculadas.Select((l, i) => new TicketPosLinea(l.CodArticulo, lineas[i].Descripcion!, l.Cantidad, l.PrecioNeto, l.TotalLinea)).ToList(),
                totales.Bruto, totales.Impuesto, totales.Total,
                pagos.Select(p => new TicketPosPago(p.CodFormaPago, NombreForma(porCodigo, p.CodFormaPago), PosCalculadora.Redondear(p.Monto))).ToList(),
                cobro.Pagado, cobro.Cambio, config.MonedaDoc!, string.Empty);
        }

        private static string NombreForma(IReadOnlyDictionary<string, FormaPago> formas, string codigo) =>
            formas.TryGetValue(codigo, out var f) ? f.Nombre : codigo;

        // ---------- Ticket y ventas del día ----------

        /// <summary>
        /// Ticket de una venta POS (también de una cancelada). Los medios son los de los cobros vigentes aplicados a la
        /// factura; el cambio no se persiste, así que sale en 0.
        /// </summary>
        public async Task<TicketPos> ObtenerTicketAsync(int facturaEntry)
        {
            var factura = await _repoFactura.ObtenerAsync(facturaEntry);
            if (factura is null || factura.DocSubType != "--" || !PosMarca.EsVentaPos(factura.Comentario))
                throw new InvalidOperationException("La venta no existe.");

            var lineas = await (await _repoDetalle.ObtenerTodoAsync())
                .Where(d => d.Entry == facturaEntry)
                .OrderBy(d => d.NoLinea)
                .ToListAsync();

            var pagosAplicados = await (await _repoPagoFactura.ObtenerTodoAsync())
                .Where(p => p.FacturaEntry == facturaEntry)
                .Select(p => p.Entry)
                .ToListAsync();
            var vigentes = pagosAplicados.Count == 0
                ? new List<int>()
                : await (await _repoPago.ObtenerTodoAsync())
                    .Where(p => pagosAplicados.Contains(p.Entry) && p.Cancelado != "S")
                    .Select(p => p.Entry)
                    .ToListAsync();
            var medios = vigentes.Count == 0
                ? new List<PagoRecibidoMedio>()
                : await (await _repoMedio.ObtenerTodoAsync())
                    .Where(m => vigentes.Contains(m.Entry))
                    .OrderBy(m => m.Entry).ThenBy(m => m.NoLinea)
                    .ToListAsync();

            var formas = medios.Count == 0
                ? new Dictionary<string, FormaPago>(StringComparer.OrdinalIgnoreCase)
                : (await (await _repoFormaPago.ObtenerTodoAsync()).ToListAsync())
                    .GroupBy(f => f.Codigo, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var nombreSerie = (await _repoSerie.ObtenerAsync(factura.Serie))?.NombreSerie ?? string.Empty;
            var pagos = medios.Select(m => new TicketPosPago(m.CodFormaPago, NombreForma(formas, m.CodFormaPago), m.Monto)).ToList();

            return new TicketPos(
                factura.Entry, factura.Serie, nombreSerie, factura.NumDoc, factura.FechaEmision ?? factura.FechaDoc ?? DateTime.MinValue,
                PosMarca.Cajero(factura.Comentario),
                factura.CodigoSn ?? string.Empty, factura.NombreSn ?? string.Empty, factura.Nit ?? string.Empty,
                lineas.Select(l => new TicketPosLinea(l.CodArticulo ?? string.Empty, l.Descripcion ?? string.Empty,
                    l.Cantidad ?? 0m, l.Precio ?? 0m, l.TotalLinea ?? 0m)).ToList(),
                factura.TotalBruto ?? 0m, factura.TotalImp ?? 0m, factura.TotalDoc ?? 0m,
                pagos, pagos.Sum(p => p.Monto), 0m, factura.MonedaDoc ?? string.Empty, string.Empty);
        }

        /// <summary>Ventas POS (factura con la marca) con fecha de hoy, más recientes primero; a lo sumo 200.</summary>
        public async Task<IReadOnlyList<VentaPosDelDia>> ObtenerVentasDelDiaAsync()
        {
            var desde = DateTime.Today;
            var hasta = desde.AddDays(1);
            var filas = await (await _repoFactura.ObtenerTodoAsync())
                .Where(f => f.DocSubType == "--"
                    && f.Comentario != null && f.Comentario.StartsWith(PosMarca.Prefijo)
                    && f.FechaDoc >= desde && f.FechaDoc < hasta)
                .OrderByDescending(f => f.Entry)
                .Take(MaximoVentasDelDia)
                .Select(f => new { f.Entry, f.Serie, f.NumDoc, f.FechaEmision, f.FechaDoc, f.CodigoSn, f.NombreSn, f.TotalDoc, f.Cancelado, f.Comentario })
                .ToListAsync();

            return filas
                .Where(f => PosMarca.EsVentaPos(f.Comentario))   // la marca distingue mayúsculas aunque la intercalación de SQL no
                .Select(f => new VentaPosDelDia(f.Entry, f.Serie, f.NumDoc, f.FechaEmision ?? f.FechaDoc ?? desde,
                    f.CodigoSn ?? string.Empty, f.NombreSn ?? string.Empty, f.TotalDoc ?? 0m, f.Cancelado == "S"))
                .ToList();
        }
    }
}
