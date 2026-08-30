using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class NumeracionDocumentoDomain : INumeracionDocumentoDomain
    {
        private readonly IRepositorioGenerico<NumeracionDocumento, string> _repoNumeracion;
        public NumeracionDocumentoDomain(IRepositorioGenerico<NumeracionDocumento, string> repoNumeracion)
        {
            _repoNumeracion = repoNumeracion;
        }

        #region async methods
        public async Task<bool> InsertarAsync(NumeracionDocumento obj)
        {
            if (await ObtenerPorCodigoAsync(obj.CodigoObj) != null)
            {
                throw new Exception($"Ya existe un registro con el código: {obj.CodigoObj}");
            }

            await _repoNumeracion.InsertarAsync(obj);
            return true;
        }
        public async Task<bool> ActualizarAsync(string codigo, NumeracionDocumento obj)
        {
            return await _repoNumeracion.ActualizarAsync(codigo, obj);
        }
        public async Task<bool> EliminarAsync(string codigo)
        {
            return await _repoNumeracion.EliminarAsync(codigo);
        }

        public async Task<NumeracionDocumento> ObtenerPorCodigoAsync(string codigo)
        {
            var queryable = await _repoNumeracion.ObtenerTodoAsync();
            var numeracion = await queryable.FirstOrDefaultAsync(x => x.CodigoObj == codigo);

            return numeracion;
        }

        public async Task<IQueryable<NumeracionDocumento>> ObtenerTodoAsync()
        {
            return await _repoNumeracion.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<NumeracionDocumento>> ObtenerContengaCodigoAsync(string codigo)
        {
            var queryable = await _repoNumeracion.ObtenerTodoAsync();
            return await queryable.Where(x => x.CodigoObj.Contains(codigo)).ToListAsync();
        }
        #endregion
    }
}
