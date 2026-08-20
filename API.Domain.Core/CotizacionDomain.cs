using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class CotizacionDomain : ICotizacionDomain
    {
        private readonly IRepositorioGenerico<Cotizacion, int> _repoGenericoCotizacion;

        public CotizacionDomain(IRepositorioGenerico<Cotizacion, int> repoGenericoCotizacion)
        {
            _repoGenericoCotizacion = repoGenericoCotizacion;
        }

        #region async methods
        public async Task<int> InsertarAsync(Cotizacion obj)
        {
            var creado = await _repoGenericoCotizacion.InsertarAsync(obj);
            return creado.Entry;
        }

        public async Task<bool> ActualizarAsync(int id, Cotizacion obj)
        {
            return await _repoGenericoCotizacion.ActualizarAsync(id, obj);
        }

        public async Task<bool> EliminarAsync(int id)
        {
            return await _repoGenericoCotizacion.EliminarAsync(id);
        }

        public async Task<Cotizacion> ObtenerAsync(int id)
        {
            return await _repoGenericoCotizacion.ObtenerAsync(id);
        }

        public async Task<IQueryable<Cotizacion>> ObtenerTodoAsync()
        {
            return await _repoGenericoCotizacion.ObtenerTodoAsync();
        }
        #endregion
    }
}
