using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class UnidadMedidaArticuloDomain : IUnidadMedidaArticuloDomain
    {
        private readonly IRepositorioGenerico<UnidadMedidaArticulo, int> _repoGenericoUnidadMedidaArticulo;

        public UnidadMedidaArticuloDomain(IRepositorioGenerico<UnidadMedidaArticulo, int> repoGenericoUnidadMedidaArticulo)
        {
            _repoGenericoUnidadMedidaArticulo = repoGenericoUnidadMedidaArticulo;
        }

        #region async methods
        public async Task<int> InsertarAsync(UnidadMedidaArticulo obj)
        {
            if (await ObtenerAsync(obj.Codigo) != null)
            {
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");
            }

            var insertado = await _repoGenericoUnidadMedidaArticulo.InsertarAsync(obj);
            return insertado.Entry;
        }

        public async Task<bool> ActualizarAsync(int codigo, UnidadMedidaArticulo obj)
        {
            var existente = await ObtenerAsync(obj.Codigo);
            if (existente != null && existente.Entry != codigo)
            {
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");
            }

            return await _repoGenericoUnidadMedidaArticulo.ActualizarAsync(codigo, obj);
        }

        public async Task<bool> EliminarAsync(int codigo)
        {
            return await _repoGenericoUnidadMedidaArticulo.EliminarAsync(codigo);
        }

        public async Task<IQueryable<UnidadMedidaArticulo>> ObtenerTodoAsync()
        {
            return await _repoGenericoUnidadMedidaArticulo.ObtenerTodoAsync();
        }

        public async Task<UnidadMedidaArticulo> ObtenerAsync(int codigo)
        {
            var queryable = await _repoGenericoUnidadMedidaArticulo.ObtenerTodoAsync();
            var unidadMedidaArticulo = await queryable.FirstOrDefaultAsync(x => x.Entry == codigo);
            return unidadMedidaArticulo;
        }

        public async Task<UnidadMedidaArticulo> ObtenerAsync(string codigo)
        {
            var queryable = await _repoGenericoUnidadMedidaArticulo.ObtenerTodoAsync();
            var unidadMedidaArticulo = await queryable.FirstOrDefaultAsync(x => x.Codigo == codigo);
            return unidadMedidaArticulo;
        }

        public async Task<IEnumerable<UnidadMedidaArticulo>> ObtenerContengaNombreAsync(string name)
        {
            var queryable = await _repoGenericoUnidadMedidaArticulo.ObtenerTodoAsync();
            var unidadMedidaArticulo = await queryable.Where(x => x.Nombre.Contains(name)).ToListAsync();
            return unidadMedidaArticulo;
        }

        #endregion
    }
}
