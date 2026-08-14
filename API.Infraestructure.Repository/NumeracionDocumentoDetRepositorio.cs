using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class NumeracionDocumentoDetRepositorio : IRepositorioGenerico<NumeracionDocumentoDet>
    {
        private readonly ApiDbTestContext _contexto;

        public NumeracionDocumentoDetRepositorio(ApiDbTestContext context)
        {
            _contexto = context;
        }

        #region async methods
        public async Task<NumeracionDocumentoDet> ObtenerAsync(int codigo)
        {
            return await _contexto.NumeracionDocumentoDets
                                    .FirstOrDefaultAsync();
        }

        public async Task<int> InsertarAsync(NumeracionDocumentoDet obj)
        {
            await _contexto.NumeracionDocumentoDets.AddAsync(obj);
            await _contexto.SaveChangesAsync();

            return 1;
        }

        public async Task<bool> ActualizarAsync(int codigo, NumeracionDocumentoDet obj)
        {
            var numeracionDocumentoDet = await _contexto
                                        .NumeracionDocumentoDets
                                        .SingleOrDefaultAsync(x => x.CodigoObj == obj.CodigoObj);

            numeracionDocumentoDet.CodigoObj = obj.CodigoObj;
            numeracionDocumentoDet.Serie = obj.Serie;
            numeracionDocumentoDet.NombreSerie = obj.NombreSerie;
            numeracionDocumentoDet.IniNumero = obj.IniNumero;
            numeracionDocumentoDet.SigNumero = obj.SigNumero;
            numeracionDocumentoDet.FinNumero = obj.FinNumero;
            numeracionDocumentoDet.IniCadena = obj.IniCadena;
            numeracionDocumentoDet.FinCadena = obj.FinCadena;
            numeracionDocumentoDet.Comentario = obj.Comentario;
            numeracionDocumentoDet.Bloqueado = obj.Bloqueado;
            numeracionDocumentoDet.CantDigitos = obj.CantDigitos;
            numeracionDocumentoDet.SubTipoDoc = obj.SubTipoDoc;

            int actualizado = await _contexto
                                    .SaveChangesAsync();

            return actualizado > 0;
        }

        public async Task<bool> EliminarAsync(int codigo)
        {
            var numeracionDocumentoDet = await _contexto
                                        .NumeracionDocumentoDets
                                        .FirstAsync();

            _contexto.NumeracionDocumentoDets.Remove(numeracionDocumentoDet);
            int eliminado = await _contexto.SaveChangesAsync();

            return eliminado > 0;
        }

        public async Task<IQueryable<NumeracionDocumentoDet>> ObtenerTodoAsync()
        {
            return _contexto.NumeracionDocumentoDets;
        }
        #endregion
    }
}
