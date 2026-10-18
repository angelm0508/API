using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class PedidoDetalleDomain : IPedidoDetalleDomain
    {
        private readonly IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)> _repoGenericoDet;

        private readonly IDocumentoBaseService _documentoBase;

        public PedidoDetalleDomain(
            IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)> repoGenericoDet,
            IDocumentoBaseService documentoBase)
        {
            _repoGenericoDet = repoGenericoDet;
            _documentoBase = documentoBase;
        }

        #region async methods
        public async Task<int> InsertarAsync(PedidoDetalle obj)
        {
            var lineasExistentes = await ObtenerPorPedidoAsync(obj.Entry);
            obj.NoLinea = lineasExistentes.Any() ? lineasExistentes.Max(x => x.NoLinea) + 1 : 1;

            var insertado = await _repoGenericoDet.InsertarAsync(obj);
            return insertado.NoLinea;
        }

        public async Task<bool> ActualizarAsync(int entry, int noLinea, PedidoDetalle obj)
        {
            await _documentoBase.ValidarLineaModificableAsync(TipoDocumentoVenta.Pedido, entry, noLinea, "modificar");
            return await _repoGenericoDet.ActualizarAsync((entry, noLinea), obj);
        }

        public async Task<bool> EliminarAsync(int entry, int noLinea)
        {
            await _documentoBase.ValidarLineaModificableAsync(TipoDocumentoVenta.Pedido, entry, noLinea, "eliminar");
            return await _repoGenericoDet.EliminarAsync((entry, noLinea));
        }

        public async Task<PedidoDetalle> ObtenerAsync(int entry, int noLinea)
        {
            return await _repoGenericoDet.ObtenerAsync((entry, noLinea));
        }

        public async Task<IQueryable<PedidoDetalle>> ObtenerTodoAsync()
        {
            return await _repoGenericoDet.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<PedidoDetalle>> ObtenerPorPedidoAsync(int entry)
        {
            var queryable = await _repoGenericoDet.ObtenerTodoAsync();
            return await queryable.Where(x => x.Entry == entry).ToListAsync();
        }
        #endregion
    }
}
