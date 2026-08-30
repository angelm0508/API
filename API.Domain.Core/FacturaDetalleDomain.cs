using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class FacturaDetalleDomain : IFacturaDetalleDomain
    {
        private readonly IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> _repoGenericoDet;

        public FacturaDetalleDomain(IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> repoGenericoDet)
        {
            _repoGenericoDet = repoGenericoDet;
        }

        #region async methods
        public async Task<int> InsertarAsync(FacturaDetalle obj)
        {
            var lineasExistentes = await ObtenerPorFacturaAsync(obj.Entry);
            obj.NoLinea = lineasExistentes.Any() ? lineasExistentes.Max(x => x.NoLinea) + 1 : 1;

            var insertado = await _repoGenericoDet.InsertarAsync(obj);
            return insertado.NoLinea;
        }

        public async Task<bool> ActualizarAsync(int entry, int noLinea, FacturaDetalle obj)
        {
            return await _repoGenericoDet.ActualizarAsync((entry, noLinea), obj);
        }

        public async Task<bool> EliminarAsync(int entry, int noLinea)
        {
            return await _repoGenericoDet.EliminarAsync((entry, noLinea));
        }

        public async Task<FacturaDetalle> ObtenerAsync(int entry, int noLinea)
        {
            return await _repoGenericoDet.ObtenerAsync((entry, noLinea));
        }

        public async Task<IQueryable<FacturaDetalle>> ObtenerTodoAsync()
        {
            return await _repoGenericoDet.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<FacturaDetalle>> ObtenerPorFacturaAsync(int entry)
        {
            var queryable = await _repoGenericoDet.ObtenerTodoAsync();
            return await queryable.Where(x => x.Entry == entry).ToListAsync();
        }
        #endregion
    }
}
