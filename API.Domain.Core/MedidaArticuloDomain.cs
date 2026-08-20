using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class MedidaArticuloDomain : IMedidaArticuloDomain
    {
        private readonly IRepositorioGenerico<MedidaArticulo, int> _repoGenericoMedidaArticulo;

        public MedidaArticuloDomain(IRepositorioGenerico<MedidaArticulo, int> repoGenericoMedidaArticulo)
        {
            _repoGenericoMedidaArticulo = repoGenericoMedidaArticulo;
        }

        #region async methods
        public async Task<int> InsertarAsync(MedidaArticulo obj)
        {
            if (await ObtenerAsync(obj.Codigo) != null)
            {
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");
            }

            var insertado = await _repoGenericoMedidaArticulo.InsertarAsync(obj);
            return insertado.Entry;
        }

        public async Task<bool> ActualizarAsync(int codigo, MedidaArticulo obj)
        {
            var existente = await ObtenerAsync(obj.Codigo);
            if (existente != null && existente.Entry != codigo)
            {
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");
            }

            return await _repoGenericoMedidaArticulo.ActualizarAsync(codigo, obj);
        }

        public async Task<bool> EliminarAsync(int codigo)
        {
            return await _repoGenericoMedidaArticulo.EliminarAsync(codigo);
        }

        public async Task<IQueryable<MedidaArticulo>> ObtenerTodoAsync()
        {
            return await _repoGenericoMedidaArticulo.ObtenerTodoAsync();
        }

        public async Task<MedidaArticulo> ObtenerAsync(int codigo)
        {
            var queryable = await _repoGenericoMedidaArticulo.ObtenerTodoAsync();
            var medidaArticulo = await queryable.FirstOrDefaultAsync(x => x.Entry == codigo);
            return medidaArticulo;
        }

        public async Task<MedidaArticulo> ObtenerAsync(string codigo)
        {
            var queryable = await _repoGenericoMedidaArticulo.ObtenerTodoAsync();
            var medidaArticulo = await queryable.FirstOrDefaultAsync(x => x.Codigo == codigo);
            return medidaArticulo;
        }

        public async Task<IEnumerable<MedidaArticulo>> ObtenerContengaNombreAsync(string name)
        {
            var queryable = await _repoGenericoMedidaArticulo.ObtenerTodoAsync();
            var medidaArticulo = await queryable.Where(x => x.Nombre.Contains(name)).ToListAsync();
            return medidaArticulo;
        }

        #endregion
    }
}
