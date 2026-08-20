using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class ListadoPrecioDomain : IListadoPrecioDomain
    {
        private readonly IRepositorioGenerico<ListadoPrecio, int> _repoGenericoListadoPrecio;

        public ListadoPrecioDomain(IRepositorioGenerico<ListadoPrecio, int> repoGenericoListadoPrecio)
        {
            _repoGenericoListadoPrecio = repoGenericoListadoPrecio;
        }

        #region async methods
        public async Task<int> InsertarAsync(ListadoPrecio obj)
        {
            if (await ObtenerAsync(obj.Nombre) != null)
            {
                throw new Exception($"Ya existe un registro con el nombre: {obj.Nombre}");
            }

            var insertado = await _repoGenericoListadoPrecio.InsertarAsync(obj);
            return insertado.Entry;
        }

        public async Task<bool> ActualizarAsync(int codigo, ListadoPrecio obj)
        {
            if (await ObtenerAsync(obj.Nombre) != null)
            {
                throw new Exception($"Ya existe un registro con el nombre: {obj.Nombre}");
            }

            return await _repoGenericoListadoPrecio.ActualizarAsync(codigo, obj);
        }

        public async Task<bool> EliminarAsync(int codigo)
        {
            return await _repoGenericoListadoPrecio.EliminarAsync(codigo);
        }

        public async Task<IQueryable<ListadoPrecio>> ObtenerTodoAsync()
        {
            return await _repoGenericoListadoPrecio.ObtenerTodoAsync();
        }

        public async Task<ListadoPrecio> ObtenerAsync(int codigo)
        {
            var queryable = await _repoGenericoListadoPrecio.ObtenerTodoAsync();
            var listadoPrecio = await queryable.FirstOrDefaultAsync(x => x.Entry == codigo);
            return listadoPrecio;
        }

        public async Task<ListadoPrecio> ObtenerAsync(string name)
        {
            var queryable = await _repoGenericoListadoPrecio.ObtenerTodoAsync();
            var listadoPrecio = await queryable.FirstOrDefaultAsync(x => x.Nombre == name);
            return listadoPrecio;
        }

        public async Task<IEnumerable<ListadoPrecio>> ObtenerContengaNombreAsync(string name)
        {
            var queryable = await _repoGenericoListadoPrecio.ObtenerTodoAsync();
            var listadoPrecio = await queryable.Where(x => x.Nombre.Contains(name)).ToListAsync();
            return listadoPrecio;
        }

        #endregion
    }
}
