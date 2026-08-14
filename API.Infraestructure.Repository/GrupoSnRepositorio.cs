using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class GrupoSnRepositorio : IRepositorioGenerico<GrupoSn>
    {
        private readonly ApiDbTestContext _contexto;

        public GrupoSnRepositorio(ApiDbTestContext context)
        {
            _contexto = context;
        }

        #region async methods
        public async Task<GrupoSn> ObtenerAsync(int codigo)
        {
            return await _contexto.GrupoSns
                                    .FirstOrDefaultAsync(x => x.Entry == codigo);
        }

        public async Task<int> InsertarAsync(GrupoSn obj)
        {
            await _contexto.GrupoSns.AddAsync(obj);
            await _contexto.SaveChangesAsync();

            return obj.Entry;
        }

        public async Task<bool> ActualizarAsync(int codigo, GrupoSn obj)
        {
            var grupoSn = await _contexto
                                        .GrupoSns
                                        .SingleOrDefaultAsync(x => x.Entry == codigo);

            grupoSn.Entry = obj.Entry;
            grupoSn.Nombre = obj.Nombre;
            grupoSn.TipoGrupo = obj.TipoGrupo;
            grupoSn.Bloqueado = obj.Bloqueado;

            int actualizado = await _contexto
                                    .SaveChangesAsync();

            return actualizado > 0;
        }

        public async Task<bool> EliminarAsync(int codigo)
        {
            var grupoSn = await _contexto
                                        .GrupoSns
                                        .SingleAsync(x => x.Entry == codigo);

            _contexto.GrupoSns.Remove(grupoSn);
            int eliminado = await _contexto.SaveChangesAsync();

            return eliminado > 0;
        }

        public async Task<IQueryable<GrupoSn>> ObtenerTodoAsync()
        {
            return _contexto.GrupoSns;
        }
        #endregion
    }
}
