using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class GrupoMedidaArticuloDomain : IGrupoMedidaArticuloDomain
    {
        private readonly IRepositorioGenerico<GrupoMedidaArticulo, int> _repoGenericoGrupoMedidaArticulo;

        public GrupoMedidaArticuloDomain(IRepositorioGenerico<GrupoMedidaArticulo, int> repoGenericoGrupoMedidaArticulo)
        {
            _repoGenericoGrupoMedidaArticulo = repoGenericoGrupoMedidaArticulo;
        }

        #region async methods
        public async Task<int> InsertarAsync(GrupoMedidaArticulo obj)
        {
            if (await ObtenerAsync(obj.Nombre) != null)
            {
                throw new Exception($"Ya existe un registro con el nombre: {obj.Nombre}");
            }

            var insertado = await _repoGenericoGrupoMedidaArticulo.InsertarAsync(obj);
            return insertado.Entry;
        }

        public async Task<bool> ActualizarAsync(int codigo, GrupoMedidaArticulo obj)
        {
            if (await ObtenerAsync(obj.Nombre) != null)
            {
                throw new Exception($"Ya existe un registro con el nombre: {obj.Nombre}");
            }

            return await _repoGenericoGrupoMedidaArticulo.ActualizarAsync(codigo, obj);
        }

        public async Task<bool> EliminarAsync(int codigo)
        {
            return await _repoGenericoGrupoMedidaArticulo.EliminarAsync(codigo);
        }

        public async Task<IQueryable<GrupoMedidaArticulo>> ObtenerTodoAsync()
        {
            return await _repoGenericoGrupoMedidaArticulo.ObtenerTodoAsync();
        }

        public async Task<GrupoMedidaArticulo> ObtenerAsync(int codigo)
        {
            var queryable = await _repoGenericoGrupoMedidaArticulo.ObtenerTodoAsync();
            var grupoMedidaArticulo = await queryable.FirstOrDefaultAsync(x => x.Entry == codigo);
            return grupoMedidaArticulo;
        }

        public async Task<GrupoMedidaArticulo> ObtenerAsync(string name)
        {
            var queryable = await _repoGenericoGrupoMedidaArticulo.ObtenerTodoAsync();
            var grupoMedidaArticulo = await queryable.FirstOrDefaultAsync(x => x.Nombre == name);
            return grupoMedidaArticulo;
        }

        public async Task<IEnumerable<GrupoMedidaArticulo>> ObtenerContengaNombreAsync(string name)
        {
            var queryable = await _repoGenericoGrupoMedidaArticulo.ObtenerTodoAsync();
            var grupoMedidaArticulo = await queryable.Where(x => x.Nombre.Contains(name)).ToListAsync();
            return grupoMedidaArticulo;
        }

        #endregion
    }
}
