using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class PedidoCompraDetalleDomain : IPedidoCompraDetalleDomain
    {
        private readonly IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)> _repoGenericoDet;

        public PedidoCompraDetalleDomain(IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)> repoGenericoDet)
        {
            _repoGenericoDet = repoGenericoDet;
        }

        #region async methods
        public async Task<int> InsertarAsync(PedidoCompraDetalle obj)
        {
            var lineasExistentes = await ObtenerPorPedidoCompraAsync(obj.Entry);
            obj.NoLinea = lineasExistentes.Any() ? lineasExistentes.Max(x => x.NoLinea) + 1 : 1;

            var insertado = await _repoGenericoDet.InsertarAsync(obj);
            return insertado.NoLinea;
        }

        public async Task<bool> ActualizarAsync(int entry, int noLinea, PedidoCompraDetalle obj)
        {
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
