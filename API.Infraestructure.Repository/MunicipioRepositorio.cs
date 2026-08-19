using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class MunicipioRepositorio : IRepositorioGenericoDos<Municipio>
    {
        private readonly ApiDbTestContext _contexto;

        public MunicipioRepositorio(ApiDbTestContext context)
        {
            _contexto = context;
        }

        #region async methods
        public async Task<bool> InsertarAsync(Municipio obj)
        {
            await _contexto.Municipios.AddAsync(obj);
            int creado = await _contexto.SaveChangesAsync();

            return creado > 0;
        }
        public async Task<bool> ActualizarAsync(string codigo, Municipio obj)
        {
            var municipio = await _contexto
                                    .Municipios
                                    .SingleOrDefaultAsync(x => x.Codigo == codigo);

            municipio.Nombre = obj.Nombre;

            int actualizado = await _contexto
                                    .SaveChangesAsync();

            return actualizado > 0;
        }
        public async Task<bool> EliminarAsync(string codigo)
        {
            var municipio = await _contexto
                                    .Municipios
                                    .SingleAsync(x => x.Codigo == codigo);

            _contexto.Municipios.Remove(municipio);
            int eliminado = await _contexto.SaveChangesAsync();

            return eliminado > 0;
        }
        public async Task<Municipio> ObtenerAsync(string codigo)
        {
            return await _contexto.Municipios
                                    .Include(x => x.DepartamentoNavigation)
                                    .FirstOrDefaultAsync(x => x.Codigo == codigo);
        }
        public async Task<IQueryable<Municipio>> ObtenerTodoAsync()
        {
            return _contexto.Municipios;
        }
        #endregion
    }
}
