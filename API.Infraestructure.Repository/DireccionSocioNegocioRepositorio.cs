using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class DireccionSocioNegocioRepositorio : IRepositorioGenericoDos<DireccionSocioNegocio>
    {
        private readonly ApiDbTestContext _contexto;

        public DireccionSocioNegocioRepositorio(ApiDbTestContext context)
        {
            _contexto = context;
        }

        #region async methods
        public async Task<bool> InsertarAsync(DireccionSocioNegocio obj)
        {
            await _contexto.DireccionSocioNegocios.AddAsync(obj);
            int creado = await _contexto.SaveChangesAsync();

            return creado > 0;
        }
        public async Task<bool> ActualizarAsync(string codigo, DireccionSocioNegocio obj)
        {
            var direccion = await _contexto
                                    .DireccionSocioNegocios
                                    .SingleOrDefaultAsync(x => x.Direccion == codigo);

            direccion.Calle = obj.Calle;
            direccion.Bloque = obj.Bloque;
            direccion.CodigoPostal = obj.CodigoPostal;
            direccion.Pais = obj.Pais;
            direccion.Municipio = obj.Municipio;
            direccion.Departamento = obj.Departamento;
            direccion.NumLinea = obj.NumLinea;
            direccion.TipoDireccion = obj.TipoDireccion;

            int actualizado = await _contexto
                                    .SaveChangesAsync();

            return actualizado > 0;
        }
        public async Task<bool> EliminarAsync(string codigo)
        {
            var direccion = await _contexto
                                    .DireccionSocioNegocios
                                    .SingleAsync(x => x.Direccion == codigo);

            _contexto.DireccionSocioNegocios.Remove(direccion);
            int eliminado = await _contexto.SaveChangesAsync();

            return eliminado > 0;
        }
        public async Task<DireccionSocioNegocio> ObtenerAsync(string codigo)
        {
            return await _contexto.DireccionSocioNegocios
                                    .Include(x => x.CodigoSnNavigation)
                                    .Include(x => x.PaisNavigation)
                                    .Include(x => x.DepartamentoNavigation)
                                    .Include(x => x.MunicipioNavigation)
                                    .FirstOrDefaultAsync(x => x.Direccion == codigo);
        }
        public async Task<IQueryable<DireccionSocioNegocio>> ObtenerTodoAsync()
        {
            return _contexto.DireccionSocioNegocios;
        }
        #endregion
    }
}
