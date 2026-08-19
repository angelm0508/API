using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class SocioNegocioRepositorio : IRepositorioGenericoDos<SocioNegocio>
    {
        private readonly ApiDbTestContext _contexto;

        public SocioNegocioRepositorio(ApiDbTestContext context)
        {
            _contexto = context;
        }

        #region async methods
        public async Task<bool> InsertarAsync(SocioNegocio obj)
        {
            await _contexto.SocioNegocios.AddAsync(obj);
            int creado = await _contexto.SaveChangesAsync();

            return creado > 0;
        }
        public async Task<bool> ActualizarAsync(string codigo, SocioNegocio obj)
        {
            var socioNegocio = await _contexto
                                    .SocioNegocios
                                    .SingleOrDefaultAsync(x => x.Codigo == codigo);

            socioNegocio.Nombre = obj.Nombre;
            socioNegocio.TipoSn = obj.TipoSn;
            socioNegocio.GrupoSn = obj.GrupoSn;
            socioNegocio.Cui = obj.Cui;
            socioNegocio.Nit = obj.Nit;
            socioNegocio.PersContacto = obj.PersContacto;
            socioNegocio.Tel1 = obj.Tel1;
            socioNegocio.Tel2 = obj.Tel2;
            socioNegocio.Descuento = obj.Descuento;
            socioNegocio.NumLstPrecio = obj.NumLstPrecio;
            socioNegocio.Email = obj.Email;
            socioNegocio.Activo = obj.Activo;

            int actualizado = await _contexto
                                    .SaveChangesAsync();

            return actualizado > 0;
        }
        public async Task<bool> EliminarAsync(string codigo)
        {
            var socioNegocio = await _contexto
                                    .SocioNegocios
                                    .SingleAsync(x => x.Codigo == codigo);

            _contexto.SocioNegocios.Remove(socioNegocio);
            int eliminado = await _contexto.SaveChangesAsync();

            return eliminado > 0;
        }
        public async Task<SocioNegocio> ObtenerAsync(string codigo)
        {
            return await _contexto.SocioNegocios
                                    .Include(x => x.GrupoSnNavigation)
                                    .Include(x => x.NumLstPrecioNavigation)
                                    .FirstOrDefaultAsync(x => x.Codigo == codigo);
        }
        public async Task<IQueryable<SocioNegocio>> ObtenerTodoAsync()
        {
            return _contexto.SocioNegocios;
        }
        #endregion
    }
}
