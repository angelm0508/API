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

        public ArticuloDomain(
            IRepositorioGenerico<Articulo, string> repoGenericoArticulo,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoGenericoNumeracion)
        {
            _repoGenericoArticulo = repoGenericoArticulo;
            _repoGenericoNumeracion = repoGenericoNumeracion;
        }

        #region async methods
        public async Task<string> InsertarAsync(Articulo obj)
        {
            var serie = await _repoGenericoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

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
            return await _repoGenericoArticulo.ActualizarAsync(sku, obj);
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
