using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class ArticuloDomain : IArticuloDomain
    {
        private readonly IRepositorioGenericoDos<Articulo> _repoGenericoArticulo;
        public ArticuloDomain(IRepositorioGenericoDos<Articulo> repoGenericoArticulo)
        {
            _repoGenericoArticulo = repoGenericoArticulo;
        }

        #region async methods
        public async Task<bool> InsertarAsync(Articulo obj)
        {
            if (await ObtenerPorCodigoAsync(obj.Codigo) != null)
            {
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");
            }

            return await _repoGenericoArticulo.InsertarAsync(obj);
        }
        public async Task<bool> ActualizarAsync(string sku, Articulo obj)
        {
            return await _repoGenericoArticulo.ActualizarAsync(sku, obj);
        }
        public async Task<bool> EliminarAsync(string sku)
        {
            return await _repoGenericoArticulo.EliminarAsync(sku);
        }

        public async Task<Articulo> ObtenerPorCodigoAsync(string sku)
        {
            var queryable = await _repoGenericoArticulo.ObtenerTodoAsync();
            var producto = await queryable.FirstOrDefaultAsync(x => x.Codigo == sku);

            return producto;
        }

        public async Task<Articulo> ObtenerPorNombreAsync(string name)
        {
            var producto = await _repoGenericoArticulo.ObtenerTodoAsync();
            return await producto.FirstOrDefaultAsync(x => x.Nombre == name);
        }
        public async Task<IQueryable<Articulo>> ObtenerTodoAsync()
        {
            return await _repoGenericoArticulo.ObtenerTodoAsync();
        }

        public async Task<IQueryable<Articulo>> ObtenerConPaginacionAsync()
        {
            return await _repoGenericoArticulo.ObtenerTodoAsync();
        }
        public async Task<IEnumerable<Articulo>> ObtenerContengaNombreAsync(string name)
        {
            var productos = await _repoGenericoArticulo.ObtenerTodoAsync();
            return await productos.Where(x => x.Nombre.Contains(name)).ToListAsync();
        }

        public async Task<IEnumerable<Articulo>> ObtenerContengaCodigoAsync(string sku)
        {
            var queryable = await _repoGenericoArticulo.ObtenerTodoAsync();
            return await queryable.Where(x => x.Codigo.Contains(sku)).ToListAsync();
        }
        #endregion
    }
}
