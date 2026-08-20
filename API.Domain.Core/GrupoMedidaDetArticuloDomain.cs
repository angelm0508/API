using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class GrupoMedidaDetArticuloDomain : IGrupoMedidaDetArticuloDomain
    {
        private readonly IRepositorioGenerico<GrupoMedidaDetArticulo, int> _repoGenericoGrupoMedidaDetArticulo;

        public GrupoMedidaDetArticuloDomain(IRepositorioGenerico<GrupoMedidaDetArticulo, int> repoGenericoGrupoMedidaDetArticulo)
        {
            _repoGenericoGrupoMedidaDetArticulo = repoGenericoGrupoMedidaDetArticulo;
        }

        #region async methods
        public async Task<int> InsertarAsync(GrupoMedidaDetArticulo obj)
        {
            var insertado = await _repoGenericoGrupoMedidaDetArticulo.InsertarAsync(obj);
            return insertado.NumLinea;
        }

        public async Task<bool> ActualizarAsync(int codigo, GrupoMedidaDetArticulo obj)
        {
            return await _repoGenericoGrupoMedidaDetArticulo.ActualizarAsync(codigo, obj);
        }

        public async Task<bool> EliminarAsync(int codigo)
        {
            return await _repoGenericoGrupoMedidaDetArticulo.EliminarAsync(codigo);
        }

        public async Task<IQueryable<GrupoMedidaDetArticulo>> ObtenerTodoAsync()
        {
            return await _repoGenericoGrupoMedidaDetArticulo.ObtenerTodoAsync();
        }

        public async Task<GrupoMedidaDetArticulo> ObtenerAsync(int codigo)
        {
            var queryable = await _repoGenericoGrupoMedidaDetArticulo.ObtenerTodoAsync();
            var grupoMedidaDetArticulo = await queryable.FirstOrDefaultAsync(x => x.GrpMedidaEntry == codigo);
            return grupoMedidaDetArticulo;
        }

        #endregion
    }
}
