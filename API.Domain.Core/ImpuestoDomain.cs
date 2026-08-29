using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class ImpuestoDomain : IImpuestoDomain
    {
        private readonly IRepositorioGenerico<Impuesto, string> _repoImpuesto;
        public ImpuestoDomain(IRepositorioGenerico<Impuesto, string> repoImpuesto)
        {
            _repoImpuesto = repoImpuesto;
        }

        #region async methods
        public async Task<bool> InsertarAsync(Impuesto obj)
        {
            if (await ObtenerPorCodigoAsync(obj.Codigo) != null)
            {
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");
            }

            await _repoImpuesto.InsertarAsync(obj);
            return true;
        }
        public async Task<bool> ActualizarAsync(string codigo, Impuesto obj)
        {
            return await _repoImpuesto.ActualizarAsync(codigo, obj);
        }
        public async Task<bool> EliminarAsync(string codigo)
        {
            return await _repoImpuesto.EliminarAsync(codigo);
        }

        public async Task<Impuesto> ObtenerPorCodigoAsync(string codigo)
        {
            var queryable = await _repoImpuesto.ObtenerTodoAsync();
            var impuesto = await queryable.FirstOrDefaultAsync(x => x.Codigo == codigo);

            return impuesto;
        }

        public async Task<IQueryable<Impuesto>> ObtenerTodoAsync()
        {
            return await _repoImpuesto.ObtenerTodoAsync();
        }
        #endregion
    }
}
