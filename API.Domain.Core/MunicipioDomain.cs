using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class MunicipioDomain : IMunicipioDomain
    {
        private readonly IRepositorioGenerico<Municipio, string> _repoMunicipio;
        public MunicipioDomain(IRepositorioGenerico<Municipio, string> repoMunicipio)
        {
            _repoMunicipio = repoMunicipio;
        }

        #region async methods
        public async Task<bool> InsertarAsync(Municipio obj)
        {
            if (await ObtenerPorCodigoAsync(obj.Codigo) != null)
            {
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");
            }

            await _repoMunicipio.InsertarAsync(obj);
            return true;
        }
        public async Task<bool> ActualizarAsync(string codigo, Municipio obj)
        {
            return await _repoMunicipio.ActualizarAsync(codigo, obj);
        }
        public async Task<bool> EliminarAsync(string codigo)
        {
            return await _repoMunicipio.EliminarAsync(codigo);
        }

        public async Task<Municipio> ObtenerPorCodigoAsync(string codigo)
        {
            var queryable = await _repoMunicipio.ObtenerTodoAsync();
            var municipio = await queryable.FirstOrDefaultAsync(x => x.Codigo == codigo);

            return municipio;
        }

        public async Task<Municipio> ObtenerPorNombreAsync(string nombre)
        {
            var municipio = await _repoMunicipio.ObtenerTodoAsync();
            return await municipio.FirstOrDefaultAsync(x => x.Nombre == nombre);
        }
        public async Task<IQueryable<Municipio>> ObtenerTodoAsync()
        {
            return await _repoMunicipio.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<Municipio>> ObtenerContengaNombreAsync(string nombre)
        {
            var municipios = await _repoMunicipio.ObtenerTodoAsync();
            return await municipios.Where(x => x.Nombre.Contains(nombre)).ToListAsync();
        }

        public async Task<IEnumerable<Municipio>> ObtenerContengaCodigoAsync(string codigo)
        {
            var queryable = await _repoMunicipio.ObtenerTodoAsync();
            return await queryable.Where(x => x.Codigo.Contains(codigo)).ToListAsync();
        }
        #endregion
    }
}
