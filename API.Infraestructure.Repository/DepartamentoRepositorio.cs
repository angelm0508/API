using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class DepartamentoRepositorio : RepositorioGenericoEfCore<Departamento, string>
    {
        public DepartamentoRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        public override async Task<Departamento?> ObtenerAsync(string id)
        {
            return await DbSet.Include(x => x.PaisNavigation).FirstOrDefaultAsync(x => x.Codigo == id);
        }
    }
}
