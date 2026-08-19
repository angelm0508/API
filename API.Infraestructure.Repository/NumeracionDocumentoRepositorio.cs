using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class NumeracionDocumentoRepositorio : IRepositorioGenericoDos<NumeracionDocumento>
    {
        private readonly ApiDbTestContext _contexto;

        public NumeracionDocumentoRepositorio(ApiDbTestContext context)
        {
            _contexto = context;
        }

        #region async methods
        public async Task<bool> InsertarAsync(NumeracionDocumento obj)
        {
            await _contexto.NumeracionDocumentos.AddAsync(obj);
            int creado = await _contexto.SaveChangesAsync();

            return creado > 0;
        }
        public async Task<bool> ActualizarAsync(string codigo, NumeracionDocumento obj)
        {
            var numeracion = await _contexto
                                    .NumeracionDocumentos
                                    .SingleOrDefaultAsync(x => x.CodigoObj == codigo);

            numeracion.SerieDfct = obj.SerieDfct;
            numeracion.DocAlias = obj.DocAlias;
            numeracion.SubTipoDoc = obj.SubTipoDoc;

            int actualizado = await _contexto
                                    .SaveChangesAsync();

            return actualizado > 0;
        }
        public async Task<bool> EliminarAsync(string codigo)
        {
            var numeracion = await _contexto
                                    .NumeracionDocumentos
                                    .SingleAsync(x => x.CodigoObj == codigo);

            _contexto.NumeracionDocumentos.Remove(numeracion);
            int eliminado = await _contexto.SaveChangesAsync();

            return eliminado > 0;
        }
        public async Task<NumeracionDocumento> ObtenerAsync(string codigo)
        {
            return await _contexto.NumeracionDocumentos
                                    .FirstOrDefaultAsync(x => x.CodigoObj == codigo);
        }
        public async Task<IQueryable<NumeracionDocumento>> ObtenerTodoAsync()
        {
            return _contexto.NumeracionDocumentos;
        }
        #endregion
    }
}
