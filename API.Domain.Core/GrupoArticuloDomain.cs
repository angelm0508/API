using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class GrupoArticuloDomain : IGrupoArticuloDomain
    {
        private readonly IRepositorioGenerico<GrupoArticulo, int> _repoGenericoGrupoArticulo;

        public GrupoArticuloDomain(IRepositorioGenerico<GrupoArticulo, int> repoGenericoGrupoArticulo)
        {
            _repoGenericoGrupoArticulo = repoGenericoGrupoArticulo;
        }

        #region async methods
        public async Task<int> InsertarAsync(GrupoArticulo obj)
        {
            if (await ObtenerAsync(obj.Nombre) != null)
            {
                throw new Exception($"Ya existe un registro con el nombre: {obj.Nombre}");
            }

            var insertado = await _repoGenericoGrupoArticulo.InsertarAsync(obj);
            return insertado.Codigo;
        }

        // public async Task<>
        public async Task<bool> ActualizarAsync(int codigo, GrupoArticulo obj)
        {
            var existente = await ObtenerAsync(codigo);
            if (existente != null && existente.Bloqueado == "S")
            {
                throw new Exception("El grupo está bloqueado y no se puede modificar.");
            }

            if (await ObtenerAsync(obj.Nombre) != null)
            {
                throw new Exception($"Ya existe un registro con el nombre: {obj.Nombre}");
            }

            return await _repoGenericoGrupoArticulo.ActualizarAsync(codigo, obj);
        }
        public async Task<bool> EliminarAsync(int codigo)
        {
            var existente = await ObtenerAsync(codigo);
            if (existente != null && existente.Bloqueado == "S")
            {
                throw new Exception("El grupo está bloqueado y no se puede eliminar.");
            }

            var queryable = await _repoGenericoGrupoArticulo.ObtenerTodoAsync();
            if (await queryable.CountAsync() <= 1)
            {
                throw new Exception("No se puede eliminar el grupo de artículo porque es el último registro disponible.");
            }

            return await _repoGenericoGrupoArticulo.EliminarAsync(codigo);
        }

        public async Task<IQueryable<GrupoArticulo>> ObtenerTodoAsync()
        {
            return await _repoGenericoGrupoArticulo.ObtenerTodoAsync();
        }

        public async Task<GrupoArticulo> ObtenerAsync(int codigo)
        {
            var queryable = await _repoGenericoGrupoArticulo.ObtenerTodoAsync();

            var grupoArticulo = await queryable.FirstOrDefaultAsync(x => x.Codigo == codigo);

            return grupoArticulo;
        }

        public async Task<GrupoArticulo> ObtenerAsync(string name)
        {
            var queryable = await _repoGenericoGrupoArticulo.ObtenerTodoAsync();

            var grupoArticulo = await queryable.FirstOrDefaultAsync(x => x.Nombre == name);

            return grupoArticulo;
        }

        public async Task<IEnumerable<GrupoArticulo>> ObtenerContengaNombreAsync(string name)
        {
            var queryable = await _repoGenericoGrupoArticulo.ObtenerTodoAsync();

            var grupoArticulo = await queryable.Where(x => x.Nombre.Contains(name)).ToListAsync();

            return grupoArticulo;
        }



        #endregion
    }
}
