using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class ArticuloDomain : IArticuloDomain
    {
        private readonly IRepositorioGenerico<Articulo, string> _repoGenericoArticulo;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoGenericoNumeracion;
        private readonly IUnidadMedidaService? _unidades;

        public ArticuloDomain(
            IRepositorioGenerico<Articulo, string> repoGenericoArticulo,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoGenericoNumeracion,
            IUnidadMedidaService? unidades = null)
        {
            _repoGenericoArticulo = repoGenericoArticulo;
            _repoGenericoNumeracion = repoGenericoNumeracion;
            _unidades = unidades;
        }

        #region async methods
        public async Task<string> InsertarAsync(Articulo obj)
        {
            var serie = await _repoGenericoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            // Antes de numerar: un rechazo por código de barras o «Mostrar en el POS» no consume consecutivo.
            await NormalizarYValidarPosAsync(obj, null);

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
                // Serie autogenerada: el consecutivo solo avanza aquí, al registrar el artículo --
                // no al solo consultar/previsualizar el código (NumeracionDocumentoDetDomain.GenerarCodigoAsync
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
                // que usa _repoGenericoArticulo; el incremento se persiste junto con el INSERT.
            }

            if (await ObtenerPorCodigoAsync(obj.Codigo) != null)
            {
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");
            }

            await _repoGenericoArticulo.InsertarAsync(obj);
            return obj.Codigo;
        }
        public async Task<bool> ActualizarAsync(string sku, Articulo obj)
        {
            await NormalizarYValidarPosAsync(obj, sku);

            if (_unidades is not null)
                await _unidades.ValidarCambioGrupoAsync(sku, obj.CodigoGrpUnidadMedida);

            if (await _repoGenericoArticulo.ActualizarAsync(sku, obj))
                return true;

            // El repositorio devuelve false si SaveChanges no afecta filas: o el artículo no existe, o no hubo
            // ningún cambio en sus columnas (p. ej. al guardar la ficha cambiando solo el mínimo, máximo o
            // bloqueo por almacén). Guardar sin cambios no es un error.
            return await _repoGenericoArticulo.ObtenerAsync(sku) != null;
        }
        private const int LongitudMaximaCodigoBarras = 50;

        /// <summary>
        /// Normaliza <c>CodigoBarras</c> (recorta; vacío = NULL, para no chocar con el índice único filtrado) y
        /// <c>MostrarEnPos</c> (vacío = «N»; solo «S»/«N»), y rechaza un código de barras que ya tiene otro artículo
        /// (sin distinguir mayúsculas, como la colación de la base). <paramref name="codigoPropio"/> excluye al propio
        /// artículo al actualizar.
        /// </summary>
        private async Task NormalizarYValidarPosAsync(Articulo obj, string? codigoPropio)
        {
            var mostrar = string.IsNullOrWhiteSpace(obj.MostrarEnPos) ? "N" : obj.MostrarEnPos.Trim().ToUpperInvariant();
            if (mostrar != "S" && mostrar != "N")
                throw new Exception("El valor de «Mostrar en el POS» no es válido.");

            var barras = string.IsNullOrWhiteSpace(obj.CodigoBarras) ? null : obj.CodigoBarras.Trim();
            if (barras is not null)
            {
                if (barras.Length > LongitudMaximaCodigoBarras)
                    throw new Exception("El código de barras no puede tener más de 50 caracteres.");

                // Igualdad directa sobre la columna (sin ToLower) para usar su índice único; la intercalación de SQL Server
                // ignora mayúsculas, así que «abc» y «ABC» siguen siendo duplicados.
                var todos = await _repoGenericoArticulo.ObtenerTodoAsync();
                var existe = await todos.AnyAsync(a => a.CodigoBarras == barras
                    && (codigoPropio == null || a.Codigo != codigoPropio));
                if (existe)
                    throw new Exception($"Ya existe un artículo con el código de barras {barras}.");
            }

            obj.CodigoBarras = barras;
            obj.MostrarEnPos = mostrar;
        }

        public async Task<bool> EliminarAsync(string sku)
        {
            return await _repoGenericoArticulo.EliminarAsync(sku);
        }

        public async Task<Articulo> ObtenerPorCodigoAsync(string sku)
        {
            var queryable = await _repoGenericoArticulo.ObtenerTodoAsync();
            var producto = await queryable.FirstOrDefaultAsync(x => x.Codigo == sku);

            return producto;
        }

        public async Task<Articulo> ObtenerPorNombreAsync(string name)
        {
            var producto = await _repoGenericoArticulo.ObtenerTodoAsync();
            return await producto.FirstOrDefaultAsync(x => x.Nombre == name);
        }
        public async Task<IQueryable<Articulo>> ObtenerTodoAsync()
        {
            return await _repoGenericoArticulo.ObtenerTodoAsync();
        }

        public async Task<IQueryable<Articulo>> ObtenerConPaginacionAsync()
        {
            return await _repoGenericoArticulo.ObtenerTodoAsync();
        }
        public async Task<IEnumerable<Articulo>> ObtenerContengaNombreAsync(string name)
        {
            var productos = await _repoGenericoArticulo.ObtenerTodoAsync();
            return await productos.Where(x => x.Nombre.Contains(name)).ToListAsync();
        }

        public async Task<IEnumerable<Articulo>> ObtenerContengaCodigoAsync(string sku)
        {
            var queryable = await _repoGenericoArticulo.ObtenerTodoAsync();
            return await queryable.Where(x => x.Codigo.Contains(sku)).ToListAsync();
        }
        #endregion
    }
}
