using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class DireccionSocioNegocioRepositorio : RepositorioGenericoEfCore<DireccionSocioNegocio, string>
    {
        public DireccionSocioNegocioRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        public override async Task<DireccionSocioNegocio?> ObtenerAsync(string id)
        {
            return await DbSet
                .Include(x => x.CodigoSnNavigation)
                .Include(x => x.PaisNavigation)
                .Include(x => x.DepartamentoNavigation)
                .Include(x => x.MunicipioNavigation)
                .FirstOrDefaultAsync(x => x.Direccion == id);
        }
    }
}
