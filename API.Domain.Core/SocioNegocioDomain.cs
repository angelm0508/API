using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class SocioNegocioDomain : ISocioNegocioDomain
    {
        private readonly IRepositorioGenerico<SocioNegocio, string> _repoSocioNegocio;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoGenericoNumeracion;

        public SocioNegocioDomain(
            IRepositorioGenerico<SocioNegocio, string> repoSocioNegocio,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoGenericoNumeracion)
        {
            _repoSocioNegocio = repoSocioNegocio;
            _repoGenericoNumeracion = repoGenericoNumeracion;
        }

        #region async methods
        public async Task<string> InsertarAsync(SocioNegocio obj)
        {
            var serie = await _repoGenericoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.Bloqueado == "S")
            {
                throw new Exception("La serie está bloqueada y no se puede usar para registrar socios de negocio.");
            }

            if (serie.Manual == "S")
            {
                // Serie manual: el código lo escribe el usuario, el consecutivo automático no aplica.
                if (string.IsNullOrWhiteSpace(obj.Codigo))
                {
                    throw new Exception("El código es requerido para series manuales.");
                }
            }
            else
            {
                // Serie autogenerada: el consecutivo solo avanza aquí, al registrar el socio -- no
                // al solo consultar/previsualizar el código (NumeracionDocumentoDetDomain.GenerarCodigoAsync
                // es de solo lectura).
                if (serie.SigNumero == null)
                {
                    throw new Exception("La serie no tiene configurado el número siguiente.");
                }

                if (serie.FinNumero.HasValue && serie.SigNumero.Value > serie.FinNumero.Value)
                {
                    throw new Exception("Se agotó la numeración disponible en esta serie.");
                }

                obj.Codigo = NumeracionDocumentoDetDomain.FormatearCodigo(serie);
                serie.SigNumero = serie.SigNumero.Value + 1;
                // Sin ActualizarAsync explícito -- "serie" ya está rastreada por el mismo DbContext
                // que usa _repoSocioNegocio; el incremento se persiste junto con el INSERT.
            }

            if (await ObtenerPorCodigoAsync(obj.Codigo) != null)
            {
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");
            }

            await _repoSocioNegocio.InsertarAsync(obj);
            return obj.Codigo;
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
