using API.Domain.Core.Reportes.Compartido;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Domain.Interface.Pos;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core.Pos
{
    /// <summary>
    /// Casos de uso del POS. Esta parte implementa la configuración y el catálogo; la venta y el ticket llegan en la Tarea 6.
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

        // Catálogo
        private const int LongitudMinimaBusqueda = 2;
        private const int MaximoBusqueda = 20;
        private const int TopeCandidatos = 100;
        private const int MaximoRapidosMarcados = 24;
        private const int MaximoMasVendidos = 12;
        private const int DiasMasVendidos = 30;
        private const int TamanoTanda = 50;

        private readonly IRepositorioGenerico<Articulo, string> _repoArticulo;
        private readonly IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)> _repoExistencia;
        private readonly IPrecioListaDomain _precios;
        private readonly ConsultaLineasVenta _consultaLineas;

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
            ConsultaLineasVenta consultaLineas)
        {
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

            if (dato.SeriePago is int seriePago && !await SerieValidaAsync(seriePago, "24", exigeSubTipoBase: false))
                throw new InvalidOperationException("La serie de pagos no es válida: debe ser una serie automática y activa de pagos recibidos.");

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

        private static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

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
            var articulo = await articulos.Where(a => a.CodigoBarras != null && a.CodigoBarras.ToLower() == c).FirstOrDefaultAsync()
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
            var lineas = await _consultaLineas.ObtenerAsync(new FiltroLineasVenta(hoy.AddDays(-DiasMasVendidos), hoy.AddDays(1), null, null, null, null));

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

        // ---------- Pendientes (Tarea 6) ----------

        public Task<TicketPos> VenderAsync(VentaPosSolicitud solicitud, string cajero) =>
            throw new NotImplementedException();

        public Task<TicketPos> ObtenerTicketAsync(int facturaEntry) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<VentaPosDelDia>> ObtenerVentasDelDiaAsync() =>
            throw new NotImplementedException();
    }
}
