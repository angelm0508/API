using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class PedidoCompraDetalleDomain : IPedidoCompraDetalleDomain
    {
        private readonly IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)> _repoGenericoDet;
        private readonly IUnidadMedidaService? _unidades;

        public PedidoCompraDetalleDomain(
            IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)> repoGenericoDet,
            IUnidadMedidaService? unidades = null)
        {
            _repoGenericoDet = repoGenericoDet;
            _unidades = unidades;
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
            return insertado.NoLinea;
        }

        public async Task<bool> ActualizarAsync(int entry, int noLinea, PedidoCompraDetalle obj)
        {
            var (uomEntry, factor, cantBase) = await UnidadesLinea.ResolverLineaAsync(_unidades, obj.CodArticulo, obj.UomEntry, obj.Cantidad);
            obj.UomEntry = uomEntry;
            obj.FactorUom = factor;
            obj.CantidadBase = cantBase;

            return await _repoGenericoDet.ActualizarAsync((entry, noLinea), obj);
        }

        public async Task<bool> EliminarAsync(int entry, int noLinea)
        {
            return await _repoGenericoDet.EliminarAsync((entry, noLinea));
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
