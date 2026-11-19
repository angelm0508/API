using API.Domain.Entity.Models;
using API.Domain.Interface.Pos;
using API.Infraestructure.Interface;

namespace API.Domain.Core.Pos
{
    /// <summary>
    /// Casos de uso del POS. Esta parte implementa la configuración; la venta, el catálogo y el ticket llegan en las Tareas 5 y 6.
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

        public PosDomain(
            IRepositorioGenerico<PosConfiguracion, int> repoConfiguracion,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoSerie,
            IRepositorioGenerico<Almacen, string> repoAlmacen,
            IRepositorioGenerico<Impuesto, string> repoImpuesto,
            IRepositorioGenerico<SocioNegocio, string> repoSocio,
            IRepositorioGenerico<FormaPago, string> repoFormaPago,
            IRepositorioGenerico<Monedum, string> repoMoneda)
        {
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

        // ---------- Pendientes (Tareas 5 y 6) ----------

        public Task<IReadOnlyList<ArticuloPos>> BuscarArticulosAsync(string? texto, string? codigoCliente) =>
            throw new NotImplementedException();

        public Task<ArticuloPos?> ObtenerArticuloPorCodigoAsync(string codigo, string? codigoCliente) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<ArticuloPos>> ObtenerRapidosAsync() =>
            throw new NotImplementedException();

        public Task<TicketPos> VenderAsync(VentaPosSolicitud solicitud, string cajero) =>
            throw new NotImplementedException();

        public Task<TicketPos> ObtenerTicketAsync(int facturaEntry) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<VentaPosDelDia>> ObtenerVentasDelDiaAsync() =>
            throw new NotImplementedException();
    }
}
