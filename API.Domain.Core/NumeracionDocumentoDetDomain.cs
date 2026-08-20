using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class NumeracionDocumentoDetDomain : INumeracionDocumentoDetDomain
    {
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoGenericoNumeracionDocumentoDet;

        public NumeracionDocumentoDetDomain(IRepositorioGenerico<NumeracionDocumentoDet, int> repoGenericoNumeracionDocumentoDet)
        {
            _repoGenericoNumeracionDocumentoDet = repoGenericoNumeracionDocumentoDet;
        }

        #region async methods
        public async Task<string> InsertarAsync(NumeracionDocumentoDet obj)
        {
            var resultado = await _repoGenericoNumeracionDocumentoDet.InsertarAsync(obj);
            return obj.CodigoObj;
        }

        public async Task<bool> ActualizarAsync(string codigoObj, NumeracionDocumentoDet obj)
        {
            var existente = await ObtenerAsync(codigoObj);
            if (existente is null)
                return false;

            return await _repoGenericoNumeracionDocumentoDet.ActualizarAsync(existente.Serie, obj);
        }

        public async Task<bool> EliminarAsync(string codigoObj)
        {
            var existente = await ObtenerAsync(codigoObj);
            if (existente is null)
                return false;

            return await _repoGenericoNumeracionDocumentoDet.EliminarAsync(existente.Serie);
        }

        public async Task<IQueryable<NumeracionDocumentoDet>> ObtenerTodoAsync()
        {
            return await _repoGenericoNumeracionDocumentoDet.ObtenerTodoAsync();
        }

        public async Task<NumeracionDocumentoDet> ObtenerAsync(string codigoObj)
        {
            var queryable = await _repoGenericoNumeracionDocumentoDet.ObtenerTodoAsync();
            var numeracionDocumentoDet = await queryable.FirstOrDefaultAsync(x => x.CodigoObj == codigoObj);
            return numeracionDocumentoDet;
        }

        #endregion
    }
}
