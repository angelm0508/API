using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class ArticuloRepositorio : RepositorioGenericoEfCore<Articulo, string>
    {
        public ArticuloRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        public override async Task<Articulo?> ObtenerAsync(string id)
        {
            return await DbSet
                .Include(x => x.CodigoGrupoNavigation)
                .Include(x => x.CodigoGrpUnidadMedidaNavigation)
                .Include(x => x.FabricanteEntryNavigation)
                .FirstOrDefaultAsync(x => x.Codigo == id);
        }
    }
}
