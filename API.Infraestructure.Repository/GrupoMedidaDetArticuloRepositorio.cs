using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class GrupoMedidaDetArticuloRepositorio : IRepositorioGenerico<GrupoMedidaDetArticulo>
    {
        private readonly ApiDbTestContext _contexto;

        public GrupoMedidaDetArticuloRepositorio(ApiDbTestContext context)
        {
            _contexto = context;
        }

        #region async methods
        public async Task<GrupoMedidaDetArticulo> ObtenerAsync(int codigo)
        {
            return await _contexto.GrupoMedidaDetArticulos
                                    .FirstOrDefaultAsync(x => x.GrpMedidaEntry == codigo);
        }

        public async Task<int> InsertarAsync(GrupoMedidaDetArticulo obj)
        {
            await _contexto.GrupoMedidaDetArticulos.AddAsync(obj);
            await _contexto.SaveChangesAsync();

            return obj.NumLinea;
        }

        public async Task<bool> ActualizarAsync(int codigo, GrupoMedidaDetArticulo obj)
        {
            var grupoMedidaDetArticulo = await _contexto
                                        .GrupoMedidaDetArticulos
                                        .SingleOrDefaultAsync(x => x.GrpMedidaEntry == codigo);

            grupoMedidaDetArticulo.GrpMedidaEntry = obj.GrpMedidaEntry;
            grupoMedidaDetArticulo.MedidaEntry = obj.MedidaEntry;
            grupoMedidaDetArticulo.CantAlternativa = obj.CantAlternativa;
            grupoMedidaDetArticulo.CantBase = obj.CantBase;
            grupoMedidaDetArticulo.NumLinea = obj.NumLinea;
            grupoMedidaDetArticulo.PesoFactor = obj.PesoFactor;
            grupoMedidaDetArticulo.UdfFactor = obj.UdfFactor;
            grupoMedidaDetArticulo.Activo = obj.Activo;

            int actualizado = await _contexto
                                    .SaveChangesAsync();

            return actualizado > 0;
        }

        public async Task<bool> EliminarAsync(int codigo)
        {
            var grupoMedidaDetArticulo = await _contexto
                                        .GrupoMedidaDetArticulos
                                        .SingleAsync(x => x.GrpMedidaEntry == codigo);

            _contexto.GrupoMedidaDetArticulos.Remove(grupoMedidaDetArticulo);
            int eliminado = await _contexto.SaveChangesAsync();

            return eliminado > 0;
        }

        public async Task<IQueryable<GrupoMedidaDetArticulo>> ObtenerTodoAsync()
        {
            return _contexto.GrupoMedidaDetArticulos;
        }
        #endregion
    }
}
