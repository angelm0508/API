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
        public async Task<int> InsertarAsync(NumeracionDocumentoDet obj)
        {
            // Serie no es autonumérica en la base de datos y ya no se le pide al usuario -- se
            // calcula el siguiente valor disponible antes de insertar.
            var queryable = await _repoGenericoNumeracionDocumentoDet.ObtenerTodoAsync();
            var maxSerie = await queryable.Select(x => (int?)x.Serie).MaxAsync() ?? 0;
            obj.Serie = maxSerie + 1;

            var insertado = await _repoGenericoNumeracionDocumentoDet.InsertarAsync(obj);
            return insertado.Serie;
        }

        public async Task<bool> ActualizarAsync(int serie, NumeracionDocumentoDet obj)
        {
            var existente = await ObtenerAsync(serie);
            if (existente != null && existente.Bloqueado == "S")
            {
                throw new Exception("La línea está bloqueada y no se puede modificar.");
            }

            return await _repoGenericoNumeracionDocumentoDet.ActualizarAsync(serie, obj);
        }

        public async Task<bool> EliminarAsync(int serie)
        {
            var existente = await ObtenerAsync(serie);
            if (existente != null && existente.Bloqueado == "S")
            {
                throw new Exception("La línea está bloqueada y no se puede eliminar.");
            }

            return await _repoGenericoNumeracionDocumentoDet.EliminarAsync(serie);
        }

        public async Task<NumeracionDocumentoDet?> ObtenerAsync(int serie)
        {
            return await _repoGenericoNumeracionDocumentoDet.ObtenerAsync(serie);
        }

        public async Task<IEnumerable<NumeracionDocumentoDet>> ObtenerPorDocumentoAsync(string codigoObj)
        {
            var queryable = await _repoGenericoNumeracionDocumentoDet.ObtenerTodoAsync();
            return await queryable.Where(x => x.CodigoObj == codigoObj).ToListAsync();
        }

        public async Task<IQueryable<NumeracionDocumentoDet>> ObtenerTodoAsync()
        {
            return await _repoGenericoNumeracionDocumentoDet.ObtenerTodoAsync();
        }

        public async Task<string> GenerarCodigoAsync(int serie)
        {
            var linea = await ObtenerAsync(serie);
            if (linea == null)
            {
                throw new Exception("La serie no existe.");
            }

            if (linea.Bloqueado == "S")
            {
                throw new Exception("La serie está bloqueada y no se puede usar para generar códigos.");
            }

            if (linea.SigNumero == null)
            {
                throw new Exception("La serie no tiene configurado el número siguiente.");
            }

            if (linea.FinNumero.HasValue && linea.SigNumero.Value > linea.FinNumero.Value)
            {
                throw new Exception("Se agotó la numeración disponible en esta serie.");
            }

            var numeroFormateado = linea.SigNumero.Value.ToString().PadLeft(linea.CantDigitos ?? 0, '0');
            var codigo = $"{linea.IniCadena}{numeroFormateado}{linea.FinCadena}";

            linea.SigNumero = linea.SigNumero.Value + 1;
            await _repoGenericoNumeracionDocumentoDet.ActualizarAsync(serie, linea);

            return codigo;
        }

        #endregion
    }
}
