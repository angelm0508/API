using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class MonedaDomain : IMonedaDomain
    {
        private readonly IRepositorioGenericoDos<Monedum> _repoMoneda;
        public MonedaDomain(IRepositorioGenericoDos<Monedum> repoMoneda)
        {
            _repoMoneda = repoMoneda;
        }

        #region async methods
        public async Task<bool> InsertarAsync(Monedum obj)
        {
            if (await ObtenerPorCodigoAsync(obj.Codigo) != null)
            {
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");
            }

            return await _repoMoneda.InsertarAsync(obj);
        }
        public async Task<bool> ActualizarAsync(string codigo, Monedum obj)
        {
            return await _repoMoneda.ActualizarAsync(codigo, obj);
        }
        public async Task<bool> EliminarAsync(string codigo)
        {
            return await _repoMoneda.EliminarAsync(codigo);
        }

        public async Task<Monedum> ObtenerPorCodigoAsync(string codigo)
        {
            var queryable = await _repoMoneda.ObtenerTodoAsync();
            var moneda = await queryable.FirstOrDefaultAsync(x => x.Codigo == codigo);

            return moneda;
        }

        public async Task<Monedum> ObtenerPorNombreAsync(string nombre)
        {
            var moneda = await _repoMoneda.ObtenerTodoAsync();
            return await moneda.FirstOrDefaultAsync(x => x.Nombre == nombre);
        }
        public async Task<IQueryable<Monedum>> ObtenerTodoAsync()
        {
            return await _repoMoneda.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<Monedum>> ObtenerContengaNombreAsync(string nombre)
        {
            var monedas = await _repoMoneda.ObtenerTodoAsync();
            return await monedas.Where(x => x.Nombre.Contains(nombre)).ToListAsync();
        }

        public async Task<IEnumerable<Monedum>> ObtenerContengaCodigoAsync(string codigo)
        {
            var queryable = await _repoMoneda.ObtenerTodoAsync();
            return await queryable.Where(x => x.Codigo.Contains(codigo)).ToListAsync();
        }
        #endregion
    }
}
