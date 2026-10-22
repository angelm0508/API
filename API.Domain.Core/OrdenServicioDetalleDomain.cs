using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class OrdenServicioDetalleDomain : IOrdenServicioDetalleDomain
    {
        private const string TipoObjetoOrden = "70";

        private readonly IRepositorioGenerico<OrdenServicio, int> _repoOrden;
        private readonly IRepositorioGenerico<OrdenServicioDetalle, (int Entry, int NoLinea)> _repoDetalle;
        private readonly IOrdenServicioLineaService _lineas;
        private readonly IInventarioAsientoService _asiento;
        private readonly IDocumentoBaseService _documentoBase;
        private readonly IEjecutorTransaccion _tx;

        public OrdenServicioDetalleDomain(
            IRepositorioGenerico<OrdenServicio, int> repoOrden,
            IRepositorioGenerico<OrdenServicioDetalle, (int Entry, int NoLinea)> repoDetalle,
            IOrdenServicioLineaService lineas,
            IInventarioAsientoService asiento,
            IDocumentoBaseService documentoBase,
            IEjecutorTransaccion tx)
        {
            _repoOrden = repoOrden;
            _repoDetalle = repoDetalle;
            _lineas = lineas;
            _asiento = asiento;
            _documentoBase = documentoBase;
            _tx = tx;
        }

        #region async methods
        public async Task<int> InsertarAsync(OrdenServicioDetalle obj)
        {
            var orden = await OrdenEditableAsync(obj.Entry);
            var existentes = await LineasAsync(obj.Entry);

            obj.NoLinea = existentes.Count == 0 ? 1 : existentes.Max(l => l.NoLinea) + 1;
            var nuevas = new List<OrdenServicioDetalle> { obj };
            await _lineas.PrepararAsync(nuevas, obj.NoLinea);   // valida y calcula antes de abrir la transacción

            return await _tx.EjecutarAsync(async () =>
            {
                await _repoDetalle.AgregarSinGuardarAsync(obj);
                await _lineas.AsentarSalidaAsync(orden.Entry, DateTime.Now, nuevas);
                _lineas.RecalcularCabecera(orden, existentes.Concat(nuevas));
                return obj.NoLinea;
            });
        }

        public async Task<bool> ActualizarAsync(int entry, int noLinea, OrdenServicioDetalle obj)
        {
            var orden = await OrdenEditableAsync(entry);
            var existente = await _repoDetalle.ObtenerAsync((entry, noLinea));
            if (existente is null)
                return false;

            // Una línea que ya se facturó (total o parcialmente) no se modifica: primero se cancela la factura.
            await _documentoBase.ValidarLineaModificableAsync(TipoDocumentoVenta.OrdenServicio, entry, noLinea, "modificar");

            var otras = (await LineasAsync(entry)).Where(l => l.NoLinea != noLinea).ToList();

            // La línea candidata se valida y se calcula aparte; recién dentro de la transacción reemplaza a la guardada.
            var candidata = new OrdenServicioDetalle
            {
                Entry = entry, NoLinea = noLinea,
                CodArticulo = obj.CodArticulo, Descripcion = obj.Descripcion, CodAlmacen = obj.CodAlmacen,
                Cantidad = obj.Cantidad, Precio = obj.Precio, PrctjeDesc = obj.PrctjeDesc,
                CodigoImpuesto = obj.CodigoImpuesto, CodMecanico = obj.CodMecanico
            };
            var lista = new List<OrdenServicioDetalle> { candidata };
            await _lineas.PrepararAsync(lista, noLinea);

            return await _tx.EjecutarAsync(async () =>
            {
                await _asiento.RevertirLineaAsync(TipoObjetoOrden, entry, noLinea);   // devuelve lo que tenía la línea

                existente.CodArticulo = candidata.CodArticulo;
                existente.Descripcion = candidata.Descripcion;
                existente.CodAlmacen = candidata.CodAlmacen;
                existente.EsInsumo = candidata.EsInsumo;
                existente.Cantidad = candidata.Cantidad;
                existente.Precio = candidata.Precio;
                existente.PrctjeDesc = candidata.PrctjeDesc;
                existente.CodigoImpuesto = candidata.CodigoImpuesto;
                existente.Impuesto = candidata.Impuesto;
                existente.TotalLinea = candidata.TotalLinea;
                existente.CodMecanico = candidata.CodMecanico;
                existente.TipoObjeto = candidata.TipoObjeto;

                await _lineas.AsentarSalidaAsync(entry, DateTime.Now, new[] { existente });   // bloquea si no alcanza el stock
                _lineas.RecalcularCabecera(orden, otras.Append(existente));
                return true;
            });
        }

        public async Task<bool> EliminarAsync(int entry, int noLinea)
        {
            var orden = await OrdenEditableAsync(entry);
            var existente = await _repoDetalle.ObtenerAsync((entry, noLinea));
            if (existente is null)
                return false;

            await _documentoBase.ValidarLineaModificableAsync(TipoDocumentoVenta.OrdenServicio, entry, noLinea, "eliminar");

            var restantes = (await LineasAsync(entry)).Where(l => l.NoLinea != noLinea).ToList();

            return await _tx.EjecutarAsync(async () =>
            {
                await _asiento.RevertirLineaAsync(TipoObjetoOrden, entry, noLinea);   // el insumo vuelve al almacén
                await _repoDetalle.EliminarAsync((entry, noLinea));
                _lineas.RecalcularCabecera(orden, restantes);
                return true;
            });
        }
        #endregion

        /// <summary>La orden debe existir, no estar cancelada y no haber sido entregada.</summary>
        private async Task<OrdenServicio> OrdenEditableAsync(int entry)
        {
            var orden = await _repoOrden.ObtenerAsync(entry)
                ?? throw new Exception("La orden de servicio no existe.");

            if (orden.Cancelado == "S")
                throw new Exception("La orden de servicio está cancelada y no se puede modificar.");
            if (orden.EstadoServicio == EstadoOrdenServicio.Entregada)
                throw new Exception("La orden de servicio ya fue entregada: solo se puede modificar el comentario.");

            return orden;
        }

        private async Task<List<OrdenServicioDetalle>> LineasAsync(int entry)
        {
            var queryable = await _repoDetalle.ObtenerTodoAsync();
            return await queryable.Where(d => d.Entry == entry).OrderBy(d => d.NoLinea).ToListAsync();
        }
    }
}
