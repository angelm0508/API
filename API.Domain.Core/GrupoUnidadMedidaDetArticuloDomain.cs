using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class GrupoUnidadMedidaDetArticuloDomain : IGrupoUnidadMedidaDetArticuloDomain
    {
        private readonly IRepositorioGenerico<GrupoUnidadMedidaDetArticulo, (int GrpMedidaEntry, int NumLinea)> _repoGenericoDet;

        public GrupoUnidadMedidaDetArticuloDomain(IRepositorioGenerico<GrupoUnidadMedidaDetArticulo, (int GrpMedidaEntry, int NumLinea)> repoGenericoDet)
        {
            _repoGenericoDet = repoGenericoDet;
        }

        #region async methods
        public async Task<int> InsertarAsync(GrupoUnidadMedidaDetArticulo obj)
        {
            var lineasExistentes = await ObtenerPorGrupoAsync(obj.GrpMedidaEntry);
            obj.NumLinea = lineasExistentes.Any() ? lineasExistentes.Max(x => x.NumLinea) + 1 : 1;

            var insertado = await _repoGenericoDet.InsertarAsync(obj);
            return insertado.NumLinea;
        }

        public async Task<bool> ActualizarAsync(int grpMedidaEntry, int numLinea, GrupoUnidadMedidaDetArticulo obj)
        {
            return await _repoGenericoDet.ActualizarAsync((grpMedidaEntry, numLinea), obj);
        }

        public async Task<bool> EliminarAsync(int grpMedidaEntry, int numLinea)
        {
            return await _repoGenericoDet.EliminarAsync((grpMedidaEntry, numLinea));
        }

        public async Task<GrupoUnidadMedidaDetArticulo> ObtenerAsync(int grpMedidaEntry, int numLinea)
        {
            return await _repoGenericoDet.ObtenerAsync((grpMedidaEntry, numLinea));
        }

        public async Task<IQueryable<GrupoUnidadMedidaDetArticulo>> ObtenerTodoAsync()
        {
            return await _repoGenericoDet.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<GrupoUnidadMedidaDetArticulo>> ObtenerPorGrupoAsync(int grpMedidaEntry)
        {
            var queryable = await _repoGenericoDet.ObtenerTodoAsync();
            return await queryable.Where(x => x.GrpMedidaEntry == grpMedidaEntry).ToListAsync();
        }
        #endregion
    }
}
