using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class GrupoMedidaArticuloRepositorio : IRepositorioGenerico<GrupoMedidaArticulo>
    {
        private readonly ApiDbTestContext _contexto;

        public GrupoMedidaArticuloRepositorio(ApiDbTestContext context)
        {
            _contexto = context;
        }

        #region async methods
        public async Task<GrupoMedidaArticulo> ObtenerAsync(int codigo)
        {
            return await _contexto.GrupoMedidaArticulos
                                    .FirstOrDefaultAsync(x => x.Entry == codigo);
        }

        public async Task<int> InsertarAsync(GrupoMedidaArticulo obj)
        {
            await _contexto.GrupoMedidaArticulos.AddAsync(obj);
            await _contexto.SaveChangesAsync();

            return obj.Entry;
        }

        public async Task<bool> ActualizarAsync(int codigo, GrupoMedidaArticulo obj)
        {
            var grupoMedidaArticulo = await _contexto
                                        .GrupoMedidaArticulos
                                        .SingleOrDefaultAsync(x => x.Entry == codigo);

            grupoMedidaArticulo.Entry = obj.Entry;
            grupoMedidaArticulo.Codigo = obj.Codigo;
            grupoMedidaArticulo.Nombre = obj.Nombre;
            grupoMedidaArticulo.BaseMedida = obj.BaseMedida;
            grupoMedidaArticulo.Bloqueado = obj.Bloqueado;

            int actualizado = await _contexto
                                    .SaveChangesAsync();

            return actualizado > 0;
        }

        public async Task<bool> EliminarAsync(int codigo)
        {
            var grupoMedidaArticulo = await _contexto
                                        .GrupoMedidaArticulos
                                        .SingleAsync(x => x.Entry == codigo);

            _contexto.GrupoMedidaArticulos.Remove(grupoMedidaArticulo);
            int eliminado = await _contexto.SaveChangesAsync();

            return eliminado > 0;
        }

        public async Task<IQueryable<GrupoMedidaArticulo>> ObtenerTodoAsync()
        {
            return _contexto.GrupoMedidaArticulos;
        }
        #endregion
    }
}
