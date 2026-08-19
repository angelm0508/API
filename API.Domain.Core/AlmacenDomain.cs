using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class AlmacenDomain : IAlmacenDomain
    {
        private readonly IRepositorioGenericoDos<Almacen> _repoAlmacen;
        public AlmacenDomain(IRepositorioGenericoDos<Almacen> repoAlmacen)
        {
            _repoAlmacen = repoAlmacen;
        }

        #region async methods
        public async Task<bool> InsertarAsync(Almacen obj)
        {
            if (await ObtenerPorCodigoAsync(obj.Codigo) != null)
            {
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");
            }

            return await _repoAlmacen.InsertarAsync(obj);
        }
        public async Task<bool> ActualizarAsync(string codigo, Almacen obj)
        {
            return await _repoAlmacen.ActualizarAsync(codigo, obj);
        }
        public async Task<bool> EliminarAsync(string codigo)
        {
            return await _repoAlmacen.EliminarAsync(codigo);
        }

        public async Task<Almacen> ObtenerPorCodigoAsync(string codigo)
        {
            var queryable = await _repoAlmacen.ObtenerTodoAsync();
            var almacen = await queryable.FirstOrDefaultAsync(x => x.Codigo == codigo);

            return almacen;
        }

        public async Task<Almacen> ObtenerPorNombreAsync(string nombre)
        {
            var almacen = await _repoAlmacen.ObtenerTodoAsync();
            return await almacen.FirstOrDefaultAsync(x => x.Nombre == nombre);
        }
        public async Task<IQueryable<Almacen>> ObtenerTodoAsync()
        {
            return await _repoAlmacen.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<Almacen>> ObtenerContengaNombreAsync(string nombre)
        {
            var almacenes = await _repoAlmacen.ObtenerTodoAsync();
            return await almacenes.Where(x => x.Nombre.Contains(nombre)).ToListAsync();
        }

        public async Task<IEnumerable<Almacen>> ObtenerContengaCodigoAsync(string codigo)
        {
            var queryable = await _repoAlmacen.ObtenerTodoAsync();
            return await queryable.Where(x => x.Codigo.Contains(codigo)).ToListAsync();
        }
        #endregion
    }
}
