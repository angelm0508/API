using API.Domain.Entity;
using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class NumeracionDocumentoRepositorio : RepositorioGenericoEfCore<NumeracionDocumento, string>
    {
        public NumeracionDocumentoRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // id = "CodigoObj" (primera fila del objeto) o "CodigoObj:SubTipoDoc" (fila exacta).
        public override async Task<NumeracionDocumento?> ObtenerAsync(string id)
        {
            var (codigo, subTipo) = NumeracionClave.Separar(id);
            return subTipo is null
                ? await DbSet.FirstOrDefaultAsync(x => x.CodigoObj == codigo)
                : await DbSet.FirstOrDefaultAsync(x => x.CodigoObj == codigo && x.SubTipoDoc == subTipo);
        }
    }
}
