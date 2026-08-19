using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class PaisDomain : IPaisDomain
    {
        private readonly IRepositorioGenericoDos<Pai> _repoGenericopais;
        public PaisDomain(IRepositorioGenericoDos<Pai> repoGenericopais)
        {
            _repoGenericopais = repoGenericopais;
        }

        #region async methods
        public async Task<bool> InsertarAsync(Pai obj)
        {
            if (await ObtenerPorCodigoAsync(obj.Codigo) != null)
            {
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");
            }

            return await _repoGenericopais.InsertarAsync(obj);
        }
        public async Task<bool> ActualizarAsync(string codigo, Pai obj)
        {
            return await _repoGenericopais.ActualizarAsync(codigo, obj);
        }
        public async Task<bool> EliminarAsync(string codigo)
        {
            return await _repoGenericopais.EliminarAsync(codigo);
        }

        public async Task<Pai> ObtenerPorCodigoAsync(string codigo)
        {
            var queryable = await _repoGenericopais.ObtenerTodoAsync();
            var pais = await queryable.FirstOrDefaultAsync(x => x.Codigo == codigo);

            return pais;
        }

        public async Task<Pai> ObtenerPorNombreAsync(string nombre)
        {
            var pais = await _repoGenericopais.ObtenerTodoAsync();
            return await pais.FirstOrDefaultAsync(x => x.Nombre == nombre);
        }
        public async Task<IQueryable<Pai>> ObtenerTodoAsync()
        {
            return await _repoGenericopais.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<Pai>> ObtenerContengaNombreAsync(string nombre)
        {
            var paises = await _repoGenericopais.ObtenerTodoAsync();
            return await paises.Where(x => x.Nombre.Contains(nombre)).ToListAsync();
        }

        public async Task<IEnumerable<Pai>> ObtenerContengaCodigoAsync(string codigo)
        {
            var queryable = await _repoGenericopais.ObtenerTodoAsync();
            return await queryable.Where(x => x.Codigo.Contains(codigo)).ToListAsync();
        }
        #endregion
    }
}
