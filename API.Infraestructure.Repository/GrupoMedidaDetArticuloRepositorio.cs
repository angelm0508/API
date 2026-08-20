using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class GrupoMedidaDetArticuloRepositorio : RepositorioGenericoEfCore<GrupoMedidaDetArticulo, int>
    {
        public GrupoMedidaDetArticuloRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        public override async Task<GrupoMedidaDetArticulo?> ObtenerAsync(int id)
        {
            return await DbSet.FirstOrDefaultAsync(x => x.GrpMedidaEntry == id);
        }
    }
}
