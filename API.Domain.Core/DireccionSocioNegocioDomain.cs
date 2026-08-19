using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class DireccionSocioNegocioDomain : IDireccionSocioNegocioDomain
    {
        private readonly IRepositorioGenericoDos<DireccionSocioNegocio> _repoDireccion;
        public DireccionSocioNegocioDomain(IRepositorioGenericoDos<DireccionSocioNegocio> repoDireccion)
        {
            _repoDireccion = repoDireccion;
        }

        #region async methods
        public async Task<bool> InsertarAsync(DireccionSocioNegocio obj)
        {
            if (await ObtenerPorCodigoAsync(obj.Direccion) != null)
            {
                throw new Exception($"Ya existe un registro con el código: {obj.Direccion}");
            }

            return await _repoDireccion.InsertarAsync(obj);
        }
        public async Task<bool> ActualizarAsync(string codigo, DireccionSocioNegocio obj)
        {
            return await _repoDireccion.ActualizarAsync(codigo, obj);
        }
        public async Task<bool> EliminarAsync(string codigo)
        {
            return await _repoDireccion.EliminarAsync(codigo);
        }

        public async Task<DireccionSocioNegocio> ObtenerPorCodigoAsync(string codigo)
        {
            var queryable = await _repoDireccion.ObtenerTodoAsync();
            var direccion = await queryable.FirstOrDefaultAsync(x => x.Direccion == codigo);

            return direccion;
        }

        public async Task<IQueryable<DireccionSocioNegocio>> ObtenerTodoAsync()
        {
            return await _repoDireccion.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<DireccionSocioNegocio>> ObtenerContengaCodigoAsync(string codigo)
        {
            var queryable = await _repoDireccion.ObtenerTodoAsync();
            return await queryable.Where(x => x.Direccion.Contains(codigo)).ToListAsync();
        }
        #endregion
    }
}
