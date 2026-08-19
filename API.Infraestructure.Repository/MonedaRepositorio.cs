using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class MonedaRepositorio : IRepositorioGenericoDos<Monedum>
    {
        private readonly ApiDbTestContext _contexto;

        public MonedaRepositorio(ApiDbTestContext context)
        {
            _contexto = context;
        }

        #region async methods
        public async Task<bool> InsertarAsync(Monedum obj)
        {
            await _contexto.Moneda.AddAsync(obj);
            int creado = await _contexto.SaveChangesAsync();

            return creado > 0;
        }
        public async Task<bool> ActualizarAsync(string codigo, Monedum obj)
        {
            var moneda = await _contexto
                                    .Moneda
                                    .SingleOrDefaultAsync(x => x.Codigo == codigo);

            moneda.Nombre = obj.Nombre;
            moneda.NombreImpresion = obj.NombreImpresion;
            moneda.Centena = obj.Centena;
            moneda.CodigoIso = obj.CodigoIso;
            moneda.TipoReondeo = obj.TipoReondeo;

            int actualizado = await _contexto
                                    .SaveChangesAsync();

            return actualizado > 0;
        }
        public async Task<bool> EliminarAsync(string codigo)
        {
            var moneda = await _contexto
                                    .Moneda
                                    .SingleAsync(x => x.Codigo == codigo);

            _contexto.Moneda.Remove(moneda);
            int eliminado = await _contexto.SaveChangesAsync();

            return eliminado > 0;
        }
        public async Task<Monedum> ObtenerAsync(string codigo)
        {
            return await _contexto.Moneda
                                    .FirstOrDefaultAsync(x => x.Codigo == codigo);
        }
        public async Task<IQueryable<Monedum>> ObtenerTodoAsync()
        {
            return _contexto.Moneda;
        }
        #endregion
    }
}
