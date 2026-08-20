using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class NumeracionDocumentoRepositorio : RepositorioGenericoEfCore<NumeracionDocumento, string>
    {
        public NumeracionDocumentoRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        public override async Task<NumeracionDocumento?> ObtenerAsync(string id)
        {
            return await DbSet.FirstOrDefaultAsync(x => x.CodigoObj == id);
        }
    }
}
