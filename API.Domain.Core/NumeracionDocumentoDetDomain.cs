using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class NumeracionDocumentoDetDomain : INumeracionDocumentoDetDomain
    {
        private readonly IRepositorioGenerico<NumeracionDocumentoDet> _repoGenericoNumeracionDocumentoDet;

        public NumeracionDocumentoDetDomain(IRepositorioGenerico<NumeracionDocumentoDet> repoGenericoNumeracionDocumentoDet)
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
            return await _repoGenericoNumeracionDocumentoDet.ActualizarAsync(codigoObj.GetHashCode(), obj);
        }

        public async Task<bool> EliminarAsync(string codigoObj)
        {
            return await _repoGenericoNumeracionDocumentoDet.EliminarAsync(codigoObj.GetHashCode());
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
