using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class SocioNegocioDomain : ISocioNegocioDomain
    {
        private const string TipoSocioCliente = "C";

        private readonly IRepositorioGenerico<SocioNegocio, string> _repoSocioNegocio;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoGenericoNumeracion;
        private readonly ICondicionPagoDomain _condicionesPago;

        public SocioNegocioDomain(
            IRepositorioGenerico<SocioNegocio, string> repoSocioNegocio,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoGenericoNumeracion,
            ICondicionPagoDomain condicionesPago)
        {
            _repoSocioNegocio = repoSocioNegocio;
            _repoGenericoNumeracion = repoGenericoNumeracion;
            _condicionesPago = condicionesPago;
        }

        #region async methods
        public async Task<string> InsertarAsync(SocioNegocio obj)
        {
            var serie = await _repoGenericoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            // Antes de numerar: una condición inválida no debe consumir el consecutivo.
            await NormalizarYValidarCondicionPagoAsync(obj);
            NormalizarYValidarCredito(obj);

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
            // Solo cambia la condición por defecto del socio: los documentos ya emitidos conservan la suya.
            await NormalizarYValidarCondicionPagoAsync(obj);
            // La actualización reemplaza todas las columnas: un límite null lo quita.
            NormalizarYValidarCredito(obj);
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
        public async Task<IQueryable<SocioNegocio>> ObtenerTodoAsync(string? tipo = null)
        {
            var queryable = await _repoSocioNegocio.ObtenerTodoAsync();
            if (tipo is "C" or "P")
                queryable = queryable.Where(x => x.TipoSn == tipo);
            return queryable;
        }

        public async Task<IEnumerable<SocioNegocio>> ObtenerContengaNombreAsync(string nombre, string? tipo = null)
        {
            var sociosNegocios = await _repoSocioNegocio.ObtenerTodoAsync();
            var filtrado = sociosNegocios.Where(x => x.Nombre.Contains(nombre));
            if (tipo is "C" or "P")
                filtrado = filtrado.Where(x => x.TipoSn == tipo);
            return await filtrado.ToListAsync();
        }

        public async Task<IEnumerable<SocioNegocio>> ObtenerContengaCodigoAsync(string codigo)
        {
            var queryable = await _repoSocioNegocio.ObtenerTodoAsync();
            return await queryable.Where(x => x.Codigo.Contains(codigo)).ToListAsync();
        }
        #endregion

        /// <summary>
        /// Vacío o en blanco se guarda como null (el sistema usará CON); si viene, debe existir y estar activa
        /// (<see cref="ICondicionPagoDomain.ObtenerActivaAsync"/> lanza "no existe" / "está inactiva").
        /// </summary>
        private async Task NormalizarYValidarCondicionPagoAsync(SocioNegocio obj)
        {
            var codigo = obj.CondicionPago?.Trim();
            if (string.IsNullOrEmpty(codigo))
            {
                obj.CondicionPago = null;
                return;
            }

            var condicion = await _condicionesPago.ObtenerActivaAsync(codigo);
            obj.CondicionPago = condicion?.Codigo ?? codigo;
        }

        /// <summary>
        /// Límite de crédito: null = sin límite, 0 = solo contado, máximo 2 decimales y solo para clientes.
        /// El bloqueo por vencido vacío o nulo se guarda como "N"; si viene debe ser "S" o "N" y en un socio
        /// que no es cliente se guarda siempre como "N".
        /// </summary>
        private static void NormalizarYValidarCredito(SocioNegocio obj)
        {
            if (obj.LimiteCredito.HasValue)
            {
                var limite = obj.LimiteCredito.Value;
                if (limite < 0)
                    throw new Exception("El límite de crédito no puede ser negativo.");
                if (decimal.Round(limite, 2) != limite)
                    throw new Exception("El límite de crédito admite como máximo 2 decimales.");
                if (obj.TipoSn != TipoSocioCliente)
                    throw new Exception("El límite de crédito solo aplica a clientes.");
            }

            var bloqueo = obj.BloquearSiVencido;
            if (string.IsNullOrWhiteSpace(bloqueo))
                bloqueo = "N";
            else if (bloqueo != "S" && bloqueo != "N")
                throw new Exception("El bloqueo por vencido debe ser S o N.");

            obj.BloquearSiVencido = obj.TipoSn == TipoSocioCliente ? bloqueo : "N";
        }
    }
}
