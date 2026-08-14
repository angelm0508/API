using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class GrupoArticuloRepositorio : IRepositorioGenerico<GrupoArticulo>
    {
        private readonly ApiDbTestContext _contexto;

        public GrupoArticuloRepositorio(ApiDbTestContext context)
        {
            _contexto = context;
        }

        #region async methods
        public async Task<GrupoArticulo> ObtenerAsync(int codigo)
        {
            return await _contexto.GrupoArticulos
                                    .FirstOrDefaultAsync(x => x.Codigo == codigo);
        }

        public async Task<int> InsertarAsync(GrupoArticulo obj)
        {
            await _contexto.GrupoArticulos.AddAsync(obj);
            await _contexto.SaveChangesAsync();

            return obj.Codigo;
        }

        public async Task<bool> ActualizarAsync(int codigo, GrupoArticulo obj)
        {
            var grupoArticulo = await _contexto
                                        .GrupoArticulos
                                        .SingleOrDefaultAsync(x => x.Codigo == codigo);

            grupoArticulo.Codigo = obj.Codigo;
            grupoArticulo.Nombre = obj.Nombre;
            grupoArticulo.Bloqueado = obj.Bloqueado;

            int actualizado = await _contexto
                                    .SaveChangesAsync();

            return actualizado > 0;
        }

        public async Task<bool> EliminarAsync(int codigo)
        {
            var grupoArticulo = await _contexto
                                        .GrupoArticulos
                                        .SingleAsync(x => x.Codigo == codigo);

            _contexto.GrupoArticulos.Remove(grupoArticulo);
            int eliminado = await _contexto.SaveChangesAsync();

            return eliminado > 0;
        }

        public async Task<IQueryable<GrupoArticulo>> ObtenerTodoAsync()
        {
            return _contexto.GrupoArticulos;
        }
        #endregion
    }
}
