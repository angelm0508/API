using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class DepartamentoDomain : IDepartamentoDomain
    {
        private readonly IRepositorioGenericoDos<Departamento> _repoDepartamento;
        public DepartamentoDomain(IRepositorioGenericoDos<Departamento> repoDepartamento)
        {
            _repoDepartamento = repoDepartamento;
        }

        #region async methods
        public async Task<bool> InsertarAsync(Departamento obj)
        {
            if (await ObtenerPorCodigoAsync(obj.Codigo) != null)
            {
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");
            }

            return await _repoDepartamento.InsertarAsync(obj);
        }
        public async Task<bool> ActualizarAsync(string codigo, Departamento obj)
        {
            return await _repoDepartamento.ActualizarAsync(codigo, obj);
        }
        public async Task<bool> EliminarAsync(string codigo)
        {
            return await _repoDepartamento.EliminarAsync(codigo);
        }

        public async Task<Departamento> ObtenerPorCodigoAsync(string codigo)
        {
            var queryable = await _repoDepartamento.ObtenerTodoAsync();
            var departamento = await queryable.FirstOrDefaultAsync(x => x.Codigo == codigo);

            return departamento;
        }

        public async Task<Departamento> ObtenerPorNombreAsync(string nombre)
        {
            var departamento = await _repoDepartamento.ObtenerTodoAsync();
            return await departamento.FirstOrDefaultAsync(x => x.Nombre == nombre);
        }
        public async Task<IQueryable<Departamento>> ObtenerTodoAsync()
        {
            return await _repoDepartamento.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<Departamento>> ObtenerContengaNombreAsync(string nombre)
        {
            var departamentos = await _repoDepartamento.ObtenerTodoAsync();
            return await departamentos.Where(x => x.Nombre.Contains(nombre)).ToListAsync();
        }

        public async Task<IEnumerable<Departamento>> ObtenerContengaCodigoAsync(string codigo)
        {
            var queryable = await _repoDepartamento.ObtenerTodoAsync();
            return await queryable.Where(x => x.Codigo.Contains(codigo)).ToListAsync();
        }
        #endregion
    }
}
