using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class SocioNegocioRepositorio : RepositorioGenericoEfCore<SocioNegocio, string>
    {
        public SocioNegocioRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        public override async Task<SocioNegocio?> ObtenerAsync(string id)
        {
            return await DbSet
                .Include(x => x.GrupoSnNavigation)
                .Include(x => x.NumLstPrecioNavigation)
                .FirstOrDefaultAsync(x => x.Codigo == id);
        }
    }
}
