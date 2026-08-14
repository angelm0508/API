using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class ListadoPrecioRepositorio : IRepositorioGenerico<ListadoPrecio>
    {
        private readonly ApiDbTestContext _contexto;

        public ListadoPrecioRepositorio(ApiDbTestContext context)
        {
            _contexto = context;
        }

        #region async methods
        public async Task<ListadoPrecio> ObtenerAsync(int codigo)
        {
            return await _contexto.ListadoPrecios
                                    .FirstOrDefaultAsync(x => x.Entry == codigo);
        }

        public async Task<int> InsertarAsync(ListadoPrecio obj)
        {
            await _contexto.ListadoPrecios.AddAsync(obj);
            await _contexto.SaveChangesAsync();

            return obj.Entry;
        }

        public async Task<bool> ActualizarAsync(int codigo, ListadoPrecio obj)
        {
            var listadoPrecio = await _contexto
                                        .ListadoPrecios
                                        .SingleOrDefaultAsync(x => x.Entry == codigo);

            listadoPrecio.Entry = obj.Entry;
            listadoPrecio.Nombre = obj.Nombre;
            listadoPrecio.Base = obj.Base;
            listadoPrecio.Factor = obj.Factor;
            listadoPrecio.MetodoRedondeo = obj.MetodoRedondeo;
            listadoPrecio.ReglaRedondeo = obj.ReglaRedondeo;
            listadoPrecio.ExtMonto = obj.ExtMonto;
            listadoPrecio.RndFrmtInt = obj.RndFrmtInt;
            listadoPrecio.RndFrmtDec = obj.RndFrmtDec;

            int actualizado = await _contexto
                                    .SaveChangesAsync();

            return actualizado > 0;
        }

        public async Task<bool> EliminarAsync(int codigo)
        {
            var listadoPrecio = await _contexto
                                        .ListadoPrecios
                                        .SingleAsync(x => x.Entry == codigo);

            _contexto.ListadoPrecios.Remove(listadoPrecio);
            int eliminado = await _contexto.SaveChangesAsync();

            return eliminado > 0;
        }

        public async Task<IQueryable<ListadoPrecio>> ObtenerTodoAsync()
        {
            return _contexto.ListadoPrecios;
        }
        #endregion
    }
}
