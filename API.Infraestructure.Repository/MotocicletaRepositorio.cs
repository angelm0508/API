using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class MotocicletaRepositorio : RepositorioGenericoEfCore<Motocicleta, int>
    {
        public MotocicletaRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        public override async Task<Motocicleta?> ObtenerAsync(int id)
        {
            return await DbSet
                .Include(x => x.CodigoSnNavigation)
                .FirstOrDefaultAsync(x => x.Entry == id);
        }

        public override Task<IQueryable<Motocicleta>> ObtenerTodoAsync()
        {
            return Task.FromResult(DbSet.Include(x => x.CodigoSnNavigation).AsQueryable());
        }
    }
}
