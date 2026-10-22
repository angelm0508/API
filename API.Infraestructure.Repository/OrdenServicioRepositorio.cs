using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class OrdenServicioRepositorio : RepositorioGenericoEfCore<OrdenServicio, int>
    {
        public OrdenServicioRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        public override async Task<OrdenServicio?> ObtenerAsync(int id)
        {
            return await DbSet
                .Include(x => x.MotoNavigation)
                .Include(x => x.MecanicoNavigation)
                .FirstOrDefaultAsync(x => x.Entry == id);
        }

        public override Task<IQueryable<OrdenServicio>> ObtenerTodoAsync()
        {
            return Task.FromResult(DbSet
                .Include(x => x.MotoNavigation)
                .Include(x => x.MecanicoNavigation)
                .AsQueryable());
        }
    }
}
