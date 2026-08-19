using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class PaisRepositorio : IRepositorioGenericoDos<Pai>
    {
        private readonly ApiDbTestContext _contexto;

        public PaisRepositorio(ApiDbTestContext context)
        {
            _contexto = context;
        }

        #region async methods
        public async Task<bool> InsertarAsync(Pai obj)
        {
            await _contexto.Pais.AddAsync(obj);
            int creado = await _contexto.SaveChangesAsync();

            return creado > 0;
        }
        public async Task<bool> ActualizarAsync(string codigo, Pai obj)
        {
            var pais = await _contexto
                                    .Pais
                                    .SingleOrDefaultAsync(x => x.Codigo == codigo);

            pais.Nombre = obj.Nombre;
            pais.Iso2codigo = obj.Iso2codigo;
            pais.Iso3codigo = obj.Iso3codigo;
            pais.Isonumerico = obj.Isonumerico;

            int actualizado = await _contexto
                                    .SaveChangesAsync();

            return actualizado > 0;
        }
        public async Task<bool> EliminarAsync(string codigo)
        {
            var pais = await _contexto
                                    .Pais
                                    .SingleAsync(x => x.Codigo == codigo);

            _contexto.Pais.Remove(pais);
            int eliminado = await _contexto.SaveChangesAsync();

            return eliminado > 0;
        }
        public async Task<Pai> ObtenerAsync(string codigo)
        {
            return await _contexto.Pais
                                    .FirstOrDefaultAsync(x => x.Codigo == codigo);
        }
        public async Task<IQueryable<Pai>> ObtenerTodoAsync()
        {
            return _contexto.Pais;
        }
        #endregion
    }
}
