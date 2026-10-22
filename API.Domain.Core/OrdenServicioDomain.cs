using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class OrdenServicioDomain : IOrdenServicioDomain
    {
        // CHECK constraint de la tabla: TipoObjeto='70'. Se fuerza siempre en el servidor.
        private const string TipoObjetoOrden = "70";
        private const string TipoSocioCliente = "C";
        private const string MsgCancelada = "La orden de servicio está cancelada y no se puede modificar.";

        private readonly IRepositorioGenerico<OrdenServicio, int> _repoOrden;
        private readonly IRepositorioGenerico<OrdenServicioDetalle, (int Entry, int NoLinea)> _repoDetalle;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoNumeracion;
        private readonly IRepositorioGenerico<SocioNegocio, string> _repoSocio;
        private readonly IRepositorioGenerico<Monedum, string> _repoMoneda;
        private readonly IRepositorioGenerico<Motocicleta, int> _repoMoto;
        private readonly IRepositorioGenerico<Mecanico, string> _repoMecanico;
        private readonly IOrdenServicioLineaService _lineas;
        private readonly IInventarioAsientoService _asiento;
        private readonly IDocumentoBaseService _documentoBase;
        private readonly IEjecutorTransaccion _tx;

        public OrdenServicioDomain(
            IRepositorioGenerico<OrdenServicio, int> repoOrden,
            IRepositorioGenerico<OrdenServicioDetalle, (int Entry, int NoLinea)> repoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoNumeracion,
            IRepositorioGenerico<SocioNegocio, string> repoSocio,
            IRepositorioGenerico<Monedum, string> repoMoneda,
            IRepositorioGenerico<Motocicleta, int> repoMoto,
            IRepositorioGenerico<Mecanico, string> repoMecanico,
            IOrdenServicioLineaService lineas,
            IInventarioAsientoService asiento,
            IDocumentoBaseService documentoBase,
            IEjecutorTransaccion tx)
        {
            _repoOrden = repoOrden;
            _repoDetalle = repoDetalle;
            _repoNumeracion = repoNumeracion;
            _repoSocio = repoSocio;
            _repoMoneda = repoMoneda;
            _repoMoto = repoMoto;
            _repoMecanico = repoMecanico;
            _lineas = lineas;
            _asiento = asiento;
            _documentoBase = documentoBase;
            _tx = tx;
        }

        #region async methods
        public async Task<int> InsertarAsync(OrdenServicio obj, IEnumerable<OrdenServicioDetalle> lineas)
        {
            var lineasList = lineas?.ToList() ?? new List<OrdenServicioDetalle>();

            obj.TipoObjeto = TipoObjetoOrden;
            obj.Cancelado = "N";                  // un documento recién registrado nunca nace cancelado
            obj.FechaCancelado = null;
            obj.FechaEntrega = null;
            obj.EstadoServicio = EstadoOrdenServicio.Recibida;
            obj.FechaDoc ??= DateTime.Now;
            obj.FechaIngreso ??= obj.FechaDoc;

            var serie = await ValidarSerieAsync(obj);
            await ValidarClienteYMonedaAsync(obj);
            await ValidarMotoYMecanicoAsync(obj);
            await _lineas.PrepararAsync(lineasList);   // antes de numerar: un error de línea no debe avanzar el consecutivo
            _lineas.RecalcularCabecera(obj, lineasList);

            AsignarNumero(obj, serie);

            return await _tx.EjecutarAsync(async () =>
            {
                await _repoOrden.InsertarAsync(obj); // Save #1: asigna obj.Entry

                var noLinea = 1;
                foreach (var linea in lineasList)
                {
                    linea.Entry = obj.Entry;
                    linea.NoLinea = noLinea++;
                    await _repoDetalle.AgregarSinGuardarAsync(linea);
                }

                await _lineas.AsentarSalidaAsync(obj.Entry, obj.FechaDoc ?? DateTime.Now, lineasList);

                return obj.Entry;
            });
            // EjecutarAsync: Save #2 (líneas + inventario) + Commit
        }

        public async Task<bool> ActualizarAsync(int id, OrdenServicio obj)
        {
            var existente = await _repoOrden.ObtenerAsync(id);
            if (existente is null)
                return false;

            if (existente.Cancelado == "S")
                throw new Exception(MsgCancelada);

            // Orden entregada: solo el comentario (replace-semantics: enviar vacío lo borra).
            if (existente.EstadoServicio == EstadoOrdenServicio.Entregada)
            {
                return await _tx.EjecutarAsync(async () =>
                {
                    await Task.CompletedTask;
                    existente.Comentario = obj.Comentario;
                    return true;
                });
            }

            // Una orden con facturas activas no cambia de cliente (primero se cancelan esas facturas).
            if (!string.Equals(existente.CodigoSn, obj.CodigoSn, StringComparison.OrdinalIgnoreCase))
                await _documentoBase.ValidarOrigenModificableAsync(TipoDocumentoVenta.OrdenServicio, id, "cambiar el cliente");

            await ValidarClienteYMonedaAsync(obj);
            await ValidarMotoYMecanicoAsync(obj);

            return await _tx.EjecutarAsync(async () =>
            {
                await Task.CompletedTask;
                existente.CodigoSn = obj.CodigoSn;
                existente.NombreSn = obj.NombreSn;
                existente.Direccion = obj.Direccion;
                existente.MonedaDoc = obj.MonedaDoc;
                existente.MotoEntry = obj.MotoEntry;
                existente.CodMecanico = obj.CodMecanico;
                existente.KmIngreso = obj.KmIngreso;
                existente.FechaDoc = obj.FechaDoc ?? existente.FechaDoc;
                existente.FechaIngreso = obj.FechaIngreso;
                existente.FechaPromesa = obj.FechaPromesa;
                existente.FallaReportada = obj.FallaReportada;
                existente.Diagnostico = obj.Diagnostico;
                existente.Observaciones = obj.Observaciones;
                existente.Comentario = obj.Comentario;
                return true;
            });
        }

        public async Task<bool> CambiarEstadoAsync(int id, string nuevoEstado)
        {
            if (!EstadoOrdenServicio.EsValido(nuevoEstado))
                throw new Exception("El estado de la orden no es válido.");

            var existente = await _repoOrden.ObtenerAsync(id);
            if (existente is null)
                return false;

            if (existente.Cancelado == "S")
                throw new Exception(MsgCancelada);

            var actual = existente.EstadoServicio;
            if (nuevoEstado == actual)
                throw new Exception($"La orden ya está en estado {EstadoOrdenServicio.Nombre(actual)}.");
            if (EstadoOrdenServicio.Orden(nuevoEstado) < EstadoOrdenServicio.Orden(actual))
                throw new Exception($"No se puede pasar de {EstadoOrdenServicio.Nombre(actual)} a {EstadoOrdenServicio.Nombre(nuevoEstado)}: la orden solo avanza.");

            return await _tx.EjecutarAsync(async () =>
            {
                await Task.CompletedTask;
                existente.EstadoServicio = nuevoEstado;
                if (nuevoEstado == EstadoOrdenServicio.Entregada)
                    existente.FechaEntrega = DateTime.Now;
                return true;
            });
        }

        public async Task<bool> CancelarAsync(int id)
        {
            var existente = await _repoOrden.ObtenerAsync(id);
            if (existente is null)
                return false;

            if (existente.Cancelado == "S")
                throw new Exception("La orden de servicio ya está cancelada.");
            if (existente.EstadoServicio == EstadoOrdenServicio.Entregada)
                throw new Exception("Una orden entregada no se puede cancelar.");

            // Una orden que ya se facturó (total o parcialmente) no se cancela: primero se cancela la factura.
            await _documentoBase.ValidarOrigenModificableAsync(TipoDocumentoVenta.OrdenServicio, id, "cancelar el documento");

            return await _tx.EjecutarAsync(async () =>
            {
                await _asiento.RevertirAsync(TipoObjetoOrden, id);   // devuelve al inventario todos los insumos
                existente.Cancelado = "S";
                existente.FechaCancelado = DateTime.Now;
                return true;
            });
        }

        public async Task<OrdenServicio?> ObtenerAsync(int id)
        {
            return await _repoOrden.ObtenerAsync(id);
        }

        public async Task<IQueryable<OrdenServicio>> ObtenerTodoAsync()
        {
            return await _repoOrden.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<OrdenServicioDetalle>> ObtenerLineasAsync(int id)
        {
            var queryable = await _repoDetalle.ObtenerTodoAsync();
            return await queryable.Where(d => d.Entry == id).OrderBy(d => d.NoLinea).ToListAsync();
        }
        #endregion

        #region validaciones
        private async Task<NumeracionDocumentoDet> ValidarSerieAsync(OrdenServicio obj)
        {
            var serie = await _repoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.CodigoObj != TipoObjetoOrden)
                throw new Exception("La serie no corresponde a órdenes de servicio.");

            if (serie.Bloqueado == "S")
                throw new Exception("La serie está bloqueada y no se puede usar para registrar órdenes de servicio.");

            if (serie.Manual == "S" && obj.NumDoc <= 0)
                throw new Exception("El número de documento es requerido para series manuales.");

            return serie;
        }

        // La numeración automática se asigna al final (después de validar todo) para no avanzar el
        // consecutivo de una serie rastreada si el documento resulta inválido.
        private static void AsignarNumero(OrdenServicio obj, NumeracionDocumentoDet serie)
        {
            if (serie.Manual == "S")
            {
                obj.NumManual = "S";
                return;
            }

            if (serie.SigNumero == null)
                throw new Exception("La serie no tiene configurado el número siguiente.");
            if (serie.FinNumero.HasValue && serie.SigNumero.Value > serie.FinNumero.Value)
                throw new Exception("Se agotó la numeración disponible en esta serie.");

            obj.NumManual = "N";
            obj.NumDoc = serie.SigNumero.Value;
            serie.SigNumero = serie.SigNumero.Value + 1; // rastreada por el mismo contexto; se persiste en el Save de la transacción
        }

        private async Task ValidarClienteYMonedaAsync(OrdenServicio obj)
        {
            if (string.IsNullOrWhiteSpace(obj.CodigoSn))
                throw new Exception("El socio de negocio es requerido.");

            var socio = await _repoSocio.ObtenerAsync(obj.CodigoSn)
                ?? throw new Exception("El socio de negocio no existe.");

            if (socio.TipoSn != TipoSocioCliente)
                throw new Exception("El socio de negocio debe ser un cliente.");

            if (socio.Activo == "N")
                throw new Exception("El socio de negocio está inactivo.");

            if (string.IsNullOrWhiteSpace(obj.NombreSn))
                obj.NombreSn = socio.Nombre;

            if (string.IsNullOrWhiteSpace(obj.MonedaDoc))
                throw new Exception("La moneda es requerida.");

            if (await _repoMoneda.ObtenerAsync(obj.MonedaDoc) is null)
                throw new Exception("La moneda no existe.");
        }

        private async Task ValidarMotoYMecanicoAsync(OrdenServicio obj)
        {
            if (obj.MotoEntry <= 0)
                throw new Exception("La motocicleta es requerida.");

            var moto = await _repoMoto.ObtenerAsync(obj.MotoEntry)
                ?? throw new Exception("La motocicleta no existe.");
            if (moto.Activo == "N")
                throw new Exception("La motocicleta está inactiva.");
            if (!string.Equals(moto.CodigoSn, obj.CodigoSn, StringComparison.OrdinalIgnoreCase))
                throw new Exception("La motocicleta no pertenece a este cliente.");

            if (!string.IsNullOrWhiteSpace(obj.CodMecanico))
            {
                var mecanico = await _repoMecanico.ObtenerAsync(obj.CodMecanico)
                    ?? throw new Exception("El mecánico no existe.");
                if (mecanico.Activo == "N")
                    throw new Exception("El mecánico está inactivo.");
            }

            if (obj.KmIngreso < 0)
                throw new Exception("El kilometraje de ingreso no puede ser negativo.");
        }
        #endregion
    }
}
