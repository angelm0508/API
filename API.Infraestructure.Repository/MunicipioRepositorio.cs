using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class MunicipioRepositorio : RepositorioGenericoEfCore<Municipio, string>
    {
        public MunicipioRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        public override async Task<Municipio?> ObtenerAsync(string id)
        {
            return await DbSet.Include(x => x.DepartamentoNavigation).FirstOrDefaultAsync(x => x.Codigo == id);
        }
    }
}
