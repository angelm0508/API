using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class FabricanteArticuloRepositorio : IRepositorioGenerico<FabricanteArticulo>
    {
        private readonly ApiDbTestContext _contexto;

        public FabricanteArticuloRepositorio(ApiDbTestContext context)
        {
            _contexto = context;
        }

        #region async methods
        public async Task<FabricanteArticulo> ObtenerAsync(int codigo)
        {
            return await _contexto.FabricanteArticulos
                                    .FirstOrDefaultAsync(x => x.Entry == codigo);
        }

        public async Task<int> InsertarAsync(FabricanteArticulo obj)
        {
            await _contexto.FabricanteArticulos.AddAsync(obj);
            await _contexto.SaveChangesAsync();

            return obj.Entry;
        }

        public async Task<bool> ActualizarAsync(int codigo, FabricanteArticulo obj)
        {
            var fabricanteArticulo = await _contexto
                                        .FabricanteArticulos
                                        .SingleOrDefaultAsync(x => x.Entry == codigo);

            fabricanteArticulo.Entry = obj.Entry;
            fabricanteArticulo.Nombre = obj.Nombre;
            fabricanteArticulo.Bloqueado = obj.Bloqueado;

            int actualizado = await _contexto
                                    .SaveChangesAsync();

            return actualizado > 0;
        }

        public async Task<bool> EliminarAsync(int codigo)
        {
            var fabricanteArticulo = await _contexto
                                        .FabricanteArticulos
                                        .SingleAsync(x => x.Entry == codigo);

            _contexto.FabricanteArticulos.Remove(fabricanteArticulo);
            int eliminado = await _contexto.SaveChangesAsync();

            return eliminado > 0;
        }

        public async Task<IQueryable<FabricanteArticulo>> ObtenerTodoAsync()
        {
            return _contexto.FabricanteArticulos;
        }
        #endregion
    }
}
