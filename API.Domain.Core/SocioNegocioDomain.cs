using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class SocioNegocioDomain : ISocioNegocioDomain
    {
        private readonly IRepositorioGenerico<SocioNegocio, string> _repoSocioNegocio;
        public SocioNegocioDomain(IRepositorioGenerico<SocioNegocio, string> repoSocioNegocio)
        {
            _repoSocioNegocio = repoSocioNegocio;
        }

        #region async methods
        public async Task<bool> InsertarAsync(SocioNegocio obj)
        {
            if (await ObtenerPorCodigoAsync(obj.Codigo) != null)
            {
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");
            }

            await _repoSocioNegocio.InsertarAsync(obj);
            return true;
        }
        public async Task<bool> ActualizarAsync(string codigo, SocioNegocio obj)
        {
            return await _repoSocioNegocio.ActualizarAsync(codigo, obj);
        }
        public async Task<bool> EliminarAsync(string codigo)
        {
            return await _repoSocioNegocio.EliminarAsync(codigo);
        }

        public async Task<SocioNegocio> ObtenerPorCodigoAsync(string codigo)
        {
            var queryable = await _repoSocioNegocio.ObtenerTodoAsync();
            var socioNegocio = await queryable.FirstOrDefaultAsync(x => x.Codigo == codigo);

            return socioNegocio;
        }

        public async Task<SocioNegocio> ObtenerPorNombreAsync(string nombre)
        {
            var socioNegocio = await _repoSocioNegocio.ObtenerTodoAsync();
            return await socioNegocio.FirstOrDefaultAsync(x => x.Nombre == nombre);
        }
        public async Task<IQueryable<SocioNegocio>> ObtenerTodoAsync()
        {
            return await _repoSocioNegocio.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<SocioNegocio>> ObtenerContengaNombreAsync(string nombre)
        {
            var sociosNegocios = await _repoSocioNegocio.ObtenerTodoAsync();
            return await sociosNegocios.Where(x => x.Nombre.Contains(nombre)).ToListAsync();
        }

        public async Task<IEnumerable<SocioNegocio>> ObtenerContengaCodigoAsync(string codigo)
        {
            var queryable = await _repoSocioNegocio.ObtenerTodoAsync();
            return await queryable.Where(x => x.Codigo.Contains(codigo)).ToListAsync();
        }
        #endregion
    }
}
