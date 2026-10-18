using API.Domain.Entity;
using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class NumeracionDocumentoRepositorio : RepositorioGenericoEfCore<NumeracionDocumento, string>
    {
        public NumeracionDocumentoRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // id = "CodigoObj" (la fila "--" del objeto; si no la tiene, la primera) o "CodigoObj:SubTipoDoc" (fila exacta).
        public override async Task<NumeracionDocumento?> ObtenerAsync(string id)
        {
            var (codigo, subTipo) = NumeracionClave.Separar(id);
            return subTipo is null
                ? await DbSet.Where(x => x.CodigoObj == codigo)
                    .OrderBy(x => x.SubTipoDoc == "--" ? 0 : 1)
                    .FirstOrDefaultAsync()
                : await DbSet.FirstOrDefaultAsync(x => x.CodigoObj == codigo && x.SubTipoDoc == subTipo);
        }
    }
}
