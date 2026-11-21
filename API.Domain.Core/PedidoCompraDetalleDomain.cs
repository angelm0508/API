using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Domain.Interface.Existencias;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class PedidoCompraDetalleDomain : IPedidoCompraDetalleDomain
    {
        private readonly IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)> _repoGenericoDet;
        private readonly IUnidadMedidaService? _unidades;
        private readonly IExistenciaCompromisoService _compromisos;

        public PedidoCompraDetalleDomain(
            IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)> repoGenericoDet,
            IUnidadMedidaService? unidades,
            IExistenciaCompromisoService compromisos)
        {
            _repoGenericoDet = repoGenericoDet;
            _unidades = unidades;
            _compromisos = compromisos;
        }

        #region async methods
        public async Task<int> InsertarAsync(PedidoCompraDetalle obj)
        {
            var (uomEntry, factor, cantBase) = await UnidadesLinea.ResolverLineaAsync(_unidades, obj.CodArticulo, obj.UomEntry, obj.Cantidad);
            obj.UomEntry = uomEntry;
            obj.FactorUom = factor;
            obj.CantidadBase = cantBase;

            var lineasExistentes = await ObtenerPorPedidoCompraAsync(obj.Entry);
            obj.NoLinea = lineasExistentes.Any() ? lineasExistentes.Max(x => x.NoLinea) + 1 : 1;

            var insertado = await _repoGenericoDet.InsertarAsync(obj);

            // Pedido (existencias): tras guardar la línea y como último paso (el aviso nunca lanza).
            await _compromisos.NotificarDocumentoAsync(TipoDocCompromiso.PedidoCompra, obj.Entry);
            return insertado.NoLinea;
        }

        public async Task<bool> ActualizarAsync(int entry, int noLinea, PedidoCompraDetalle obj)
        {
            var (uomEntry, factor, cantBase) = await UnidadesLinea.ResolverLineaAsync(_unidades, obj.CodArticulo, obj.UomEntry, obj.Cantidad);
            obj.UomEntry = uomEntry;
            obj.FactorUom = factor;
            obj.CantidadBase = cantBase;

            // Pedido (existencias): los pares se leen ANTES de guardar; si la línea cambia de artículo o almacén,
            // el par viejo ya no se puede deducir del documento guardado y quedaría inflado.
            var paresAntes = await _compromisos.ObtenerParesAsync(TipoDocCompromiso.PedidoCompra, entry);

            var actualizado = await _repoGenericoDet.ActualizarAsync((entry, noLinea), obj);
            if (actualizado)
            {
                // Pares viejos + pares del documento ya guardado (los avisos nunca lanzan; van al final).
                await _compromisos.NotificarParesAsync(paresAntes);
                await _compromisos.NotificarDocumentoAsync(TipoDocCompromiso.PedidoCompra, entry);
            }
            return actualizado;
        }

        public async Task<bool> EliminarAsync(int entry, int noLinea)
        {
            // Pedido (existencias): los pares (incluido el de la línea que se borra) se leen ANTES de borrar; después
            // la línea ya no existe y, si era la única de su artículo/almacén, ese par nunca se recalcularía.
            var pares = await _compromisos.ObtenerParesAsync(TipoDocCompromiso.PedidoCompra, entry);

            var eliminado = await _repoGenericoDet.EliminarAsync((entry, noLinea));
            if (eliminado)
                await _compromisos.NotificarParesAsync(pares);
            return eliminado;
        }

        public async Task<PedidoCompraDetalle> ObtenerAsync(int entry, int noLinea)
        {
            return await _repoGenericoDet.ObtenerAsync((entry, noLinea));
        }

        public async Task<IQueryable<PedidoCompraDetalle>> ObtenerTodoAsync()
        {
            return await _repoGenericoDet.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<PedidoCompraDetalle>> ObtenerPorPedidoCompraAsync(int entry)
        {
            var queryable = await _repoGenericoDet.ObtenerTodoAsync();
            return await queryable.Where(x => x.Entry == entry).ToListAsync();
        }
        #endregion
    }
}
