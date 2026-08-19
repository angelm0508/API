using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class DepartamentoRepositorio : IRepositorioGenericoDos<Departamento>
    {
        private readonly ApiDbTestContext _contexto;

        public DepartamentoRepositorio(ApiDbTestContext context)
        {
            _contexto = context;
        }

        #region async methods
        public async Task<bool> InsertarAsync(Departamento obj)
        {
            await _contexto.Departamentos.AddAsync(obj);
            int creado = await _contexto.SaveChangesAsync();

            return creado > 0;
        }
        public async Task<bool> ActualizarAsync(string codigo, Departamento obj)
        {
            var departamento = await _contexto
                                    .Departamentos
                                    .SingleOrDefaultAsync(x => x.Codigo == codigo);

            departamento.Nombre = obj.Nombre;

            int actualizado = await _contexto
                                    .SaveChangesAsync();

            return actualizado > 0;
        }
        public async Task<bool> EliminarAsync(string codigo)
        {
            var departamento = await _contexto
                                    .Departamentos
                                    .SingleAsync(x => x.Codigo == codigo);

            _contexto.Departamentos.Remove(departamento);
            int eliminado = await _contexto.SaveChangesAsync();

            return eliminado > 0;
        }
        public async Task<Departamento> ObtenerAsync(string codigo)
        {
            return await _contexto.Departamentos
                                    .Include(x => x.PaisNavigation)
                                    .FirstOrDefaultAsync(x => x.Codigo == codigo);
        }
        public async Task<IQueryable<Departamento>> ObtenerTodoAsync()
        {
            return _contexto.Departamentos;
        }
        #endregion
    }
}
