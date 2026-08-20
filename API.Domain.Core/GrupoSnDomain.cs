using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class GrupoSnDomain : IGrupoSnDomain
    {
        private readonly IRepositorioGenerico<GrupoSn, int> _repoGenericoGrupoSn;

        public GrupoSnDomain(IRepositorioGenerico<GrupoSn, int> repoGenericoGrupoSn)
        {
            _repoGenericoGrupoSn = repoGenericoGrupoSn;
        }

        #region async methods
        public async Task<int> InsertarAsync(GrupoSn obj)
        {
            if (await ObtenerAsync(obj.Nombre) != null)
            {
                throw new Exception($"Ya existe un registro con el nombre: {obj.Nombre}");
            }

            var insertado = await _repoGenericoGrupoSn.InsertarAsync(obj);
            return insertado.Entry;
        }

        public async Task<bool> ActualizarAsync(int codigo, GrupoSn obj)
        {
            if (await ObtenerAsync(obj.Nombre) != null)
            {
                throw new Exception($"Ya existe un registro con el nombre: {obj.Nombre}");
            }

            return await _repoGenericoGrupoSn.ActualizarAsync(codigo, obj);
        }

        public async Task<bool> EliminarAsync(int codigo)
        {
            return await _repoGenericoGrupoSn.EliminarAsync(codigo);
        }

        public async Task<IQueryable<GrupoSn>> ObtenerTodoAsync()
        {
            return await _repoGenericoGrupoSn.ObtenerTodoAsync();
        }

        public async Task<GrupoSn> ObtenerAsync(int codigo)
        {
            var queryable = await _repoGenericoGrupoSn.ObtenerTodoAsync();
            var grupoSn = await queryable.FirstOrDefaultAsync(x => x.Entry == codigo);
            return grupoSn;
        }

        public async Task<GrupoSn> ObtenerAsync(string name)
        {
            var queryable = await _repoGenericoGrupoSn.ObtenerTodoAsync();
            var grupoSn = await queryable.FirstOrDefaultAsync(x => x.Nombre == name);
            return grupoSn;
        }

        public async Task<IEnumerable<GrupoSn>> ObtenerContengaNombreAsync(string name)
        {
            var queryable = await _repoGenericoGrupoSn.ObtenerTodoAsync();
            var grupoSn = await queryable.Where(x => x.Nombre.Contains(name)).ToListAsync();
            return grupoSn;
        }

        #endregion
    }
}
