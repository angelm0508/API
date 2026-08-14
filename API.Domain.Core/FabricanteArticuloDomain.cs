using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class FabricanteArticuloDomain : IFabricanteArticuloDomain
    {
        private readonly IRepositorioGenerico<FabricanteArticulo> _repoGenericoFabricanteArticulo;

        public FabricanteArticuloDomain(IRepositorioGenerico<FabricanteArticulo> repoGenericoFabricanteArticulo)
        {
            _repoGenericoFabricanteArticulo = repoGenericoFabricanteArticulo;
        }

        #region async methods
        public async Task<int> InsertarAsync(FabricanteArticulo obj)
        {
            if (await ObtenerAsync(obj.Nombre) != null)
            {
                throw new Exception($"Ya existe un registro con el nombre: {obj.Nombre}");
            }

            return await _repoGenericoFabricanteArticulo.InsertarAsync(obj);
        }

        public async Task<bool> ActualizarAsync(int codigo, FabricanteArticulo obj)
        {
            if (await ObtenerAsync(obj.Nombre) != null)
            {
                throw new Exception($"Ya existe un registro con el nombre: {obj.Nombre}");
            }

            return await _repoGenericoFabricanteArticulo.ActualizarAsync(codigo, obj);
        }

        public async Task<bool> EliminarAsync(int codigo)
        {
            return await _repoGenericoFabricanteArticulo.EliminarAsync(codigo);
        }

        public async Task<IQueryable<FabricanteArticulo>> ObtenerTodoAsync()
        {
            return await _repoGenericoFabricanteArticulo.ObtenerTodoAsync();
        }

        public async Task<FabricanteArticulo> ObtenerAsync(int codigo)
        {
            var queryable = await _repoGenericoFabricanteArticulo.ObtenerTodoAsync();
            var fabricanteArticulo = await queryable.FirstOrDefaultAsync(x => x.Entry == codigo);
            return fabricanteArticulo;
        }

        public async Task<FabricanteArticulo> ObtenerAsync(string name)
        {
            var queryable = await _repoGenericoFabricanteArticulo.ObtenerTodoAsync();
            var fabricanteArticulo = await queryable.FirstOrDefaultAsync(x => x.Nombre == name);
            return fabricanteArticulo;
        }

        public async Task<IEnumerable<FabricanteArticulo>> ObtenerContengaNombreAsync(string name)
        {
            var queryable = await _repoGenericoFabricanteArticulo.ObtenerTodoAsync();
            var fabricanteArticulo = await queryable.Where(x => x.Nombre.Contains(name)).ToListAsync();
            return fabricanteArticulo;
        }

        #endregion
    }
}
