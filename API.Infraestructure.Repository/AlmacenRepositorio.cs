using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class AlmacenRepositorio : IRepositorioGenericoDos<Almacen>
    {
        private readonly ApiDbTestContext _contexto;

        public AlmacenRepositorio(ApiDbTestContext context)
        {
            _contexto = context;
        }

        #region async methods
        public async Task<bool> InsertarAsync(Almacen obj)
        {
            await _contexto.Almacens.AddAsync(obj);
            int creado = await _contexto.SaveChangesAsync();

            return creado > 0;
        }
        public async Task<bool> ActualizarAsync(string codigo, Almacen obj)
        {
            var almacen = await _contexto
                                    .Almacens
                                    .SingleOrDefaultAsync(x => x.Codigo == codigo);

            almacen.Nombre = obj.Nombre;
            almacen.Activo = obj.Activo;
            almacen.Calle = obj.Calle;
            almacen.CodigoPostal = obj.CodigoPostal;
            almacen.Pais = obj.Pais;
            almacen.Municipio = obj.Municipio;
            almacen.Departamento = obj.Departamento;
            almacen.Bloqueado = obj.Bloqueado;

            int actualizado = await _contexto
                                    .SaveChangesAsync();

            return actualizado > 0;
        }
        public async Task<bool> EliminarAsync(string codigo)
        {
            var almacen = await _contexto
                                    .Almacens
                                    .SingleAsync(x => x.Codigo == codigo);

            _contexto.Almacens.Remove(almacen);
            int eliminado = await _contexto.SaveChangesAsync();

            return eliminado > 0;
        }
        public async Task<Almacen> ObtenerAsync(string codigo)
        {
            return await _contexto.Almacens
                                    .Include(x => x.PaisNavigation)
                                    .Include(x => x.DepartamentoNavigation)
                                    .Include(x => x.MunicipioNavigation)
                                    .FirstOrDefaultAsync(x => x.Codigo == codigo);
        }
        public async Task<IQueryable<Almacen>> ObtenerTodoAsync()
        {
            return _contexto.Almacens;
        }
        #endregion
    }
}
