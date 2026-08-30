using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class AlmacenDomain : IAlmacenDomain
    {
        private readonly IRepositorioGenerico<Almacen, string> _repoAlmacen;
        public AlmacenDomain(IRepositorioGenerico<Almacen, string> repoAlmacen)
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

            await _repoAlmacen.InsertarAsync(obj);
            return true;
        }
        public async Task<bool> ActualizarAsync(string codigo, Almacen obj)
        {
            var existente = await ObtenerPorCodigoAsync(codigo);
            if (existente != null && existente.Bloqueado == "S")
            {
                throw new Exception("El almacén está bloqueado y no se puede modificar.");
            }

            return await _repoAlmacen.ActualizarAsync(codigo, obj);
        }
        public async Task<bool> EliminarAsync(string codigo)
        {
            var existente = await ObtenerPorCodigoAsync(codigo);
            if (existente != null && existente.Bloqueado == "S")
            {
                throw new Exception("El almacén está bloqueado y no se puede eliminar.");
            }

            var queryable = await _repoAlmacen.ObtenerTodoAsync();
            if (await queryable.CountAsync() <= 1)
            {
                throw new Exception("No se puede eliminar el almacén porque es el último registro disponible.");
            }

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
