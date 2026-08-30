using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class GrupoUnidadMedidaArticuloDomain : IGrupoUnidadMedidaArticuloDomain
    {
        private readonly IRepositorioGenerico<GrupoUnidadMedidaArticulo, int> _repoGenericoGrupoUnidadMedidaArticulo;

        public GrupoUnidadMedidaArticuloDomain(IRepositorioGenerico<GrupoUnidadMedidaArticulo, int> repoGenericoGrupoUnidadMedidaArticulo)
        {
            _repoGenericoGrupoUnidadMedidaArticulo = repoGenericoGrupoUnidadMedidaArticulo;
        }

        #region async methods
        public async Task<int> InsertarAsync(GrupoUnidadMedidaArticulo obj)
        {
            if (await ObtenerPorCodigoAsync(obj.Codigo) != null)
            {
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");
            }

            var insertado = await _repoGenericoGrupoUnidadMedidaArticulo.InsertarAsync(obj);
            return insertado.Entry;
        }

        public async Task<bool> ActualizarAsync(int codigo, GrupoUnidadMedidaArticulo obj)
        {
            var existente = await ObtenerAsync(codigo);
            if (existente != null && existente.Bloqueado == "S")
            {
                throw new Exception("El grupo está bloqueado y no se puede modificar.");
            }

            var duplicado = await ObtenerPorCodigoAsync(obj.Codigo);
            if (duplicado != null && duplicado.Entry != codigo)
            {
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");
            }

            return await _repoGenericoGrupoUnidadMedidaArticulo.ActualizarAsync(codigo, obj);
        }

        private async Task<GrupoUnidadMedidaArticulo?> ObtenerPorCodigoAsync(string? codigo)
        {
            var queryable = await _repoGenericoGrupoUnidadMedidaArticulo.ObtenerTodoAsync();
            return await queryable.FirstOrDefaultAsync(x => x.Codigo == codigo);
        }

        public async Task<bool> EliminarAsync(int codigo)
        {
            var existente = await ObtenerAsync(codigo);
            if (existente != null && existente.Bloqueado == "S")
            {
                throw new Exception("El grupo está bloqueado y no se puede eliminar.");
            }

            return await _repoGenericoGrupoUnidadMedidaArticulo.EliminarAsync(codigo);
        }

        public async Task<IQueryable<GrupoUnidadMedidaArticulo>> ObtenerTodoAsync()
        {
            return await _repoGenericoGrupoUnidadMedidaArticulo.ObtenerTodoAsync();
        }

        public async Task<GrupoUnidadMedidaArticulo> ObtenerAsync(int codigo)
        {
            var queryable = await _repoGenericoGrupoUnidadMedidaArticulo.ObtenerTodoAsync();
            var grupoUnidadMedidaArticulo = await queryable.FirstOrDefaultAsync(x => x.Entry == codigo);
            return grupoUnidadMedidaArticulo;
        }

        public async Task<GrupoUnidadMedidaArticulo> ObtenerAsync(string name)
        {
            var queryable = await _repoGenericoGrupoUnidadMedidaArticulo.ObtenerTodoAsync();
            var grupoUnidadMedidaArticulo = await queryable.FirstOrDefaultAsync(x => x.Nombre == name);
            return grupoUnidadMedidaArticulo;
        }

        public async Task<IEnumerable<GrupoUnidadMedidaArticulo>> ObtenerContengaNombreAsync(string name)
        {
            var queryable = await _repoGenericoGrupoUnidadMedidaArticulo.ObtenerTodoAsync();
            var grupoUnidadMedidaArticulo = await queryable.Where(x => x.Nombre.Contains(name)).ToListAsync();
            return grupoUnidadMedidaArticulo;
        }

        #endregion
    }
}
