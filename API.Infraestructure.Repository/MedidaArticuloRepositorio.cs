using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class MedidaArticuloRepositorio : IRepositorioGenerico<MedidaArticulo>
    {
        private readonly ApiDbTestContext _contexto;

        public MedidaArticuloRepositorio(ApiDbTestContext context)
        {
            _contexto = context;
        }

        #region async methods
        public async Task<MedidaArticulo> ObtenerAsync(int codigo)
        {
            return await _contexto.MedidaArticulos
                                    .FirstOrDefaultAsync(x => x.Entry == codigo);
        }

        public async Task<int> InsertarAsync(MedidaArticulo obj)
        {
            await _contexto.MedidaArticulos.AddAsync(obj);
            await _contexto.SaveChangesAsync();

            return obj.Entry;
        }

        public async Task<bool> ActualizarAsync(int codigo, MedidaArticulo obj)
        {
            var medidaArticulo = await _contexto
                                        .MedidaArticulos
                                        .SingleOrDefaultAsync(x => x.Entry == codigo);

            medidaArticulo.Entry = obj.Entry;
            medidaArticulo.Codigo = obj.Codigo;
            medidaArticulo.Nombre = obj.Nombre;
            medidaArticulo.Largo = obj.Largo;
            medidaArticulo.Ancho = obj.Ancho;
            medidaArticulo.Altura = obj.Altura;
            medidaArticulo.Volumen = obj.Volumen;
            medidaArticulo.Peso = obj.Peso;
            medidaArticulo.Bloqueado = obj.Bloqueado;

            int actualizado = await _contexto
                                    .SaveChangesAsync();

            return actualizado > 0;
        }

        public async Task<bool> EliminarAsync(int codigo)
        {
            var medidaArticulo = await _contexto
                                        .MedidaArticulos
                                        .SingleAsync(x => x.Entry == codigo);

            _contexto.MedidaArticulos.Remove(medidaArticulo);
            int eliminado = await _contexto.SaveChangesAsync();

            return eliminado > 0;
        }

        public async Task<IQueryable<MedidaArticulo>> ObtenerTodoAsync()
        {
            return _contexto.MedidaArticulos;
        }
        #endregion
    }
}
