using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class AlmacenRepositorio : RepositorioGenericoEfCore<Almacen, string>
    {
        public AlmacenRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        public override async Task<Almacen?> ObtenerAsync(string id)
        {
            return await DbSet
                .Include(x => x.PaisNavigation)
                .Include(x => x.DepartamentoNavigation)
                .Include(x => x.MunicipioNavigation)
                .FirstOrDefaultAsync(x => x.Codigo == id);
        }
    }
}
