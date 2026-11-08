using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class SalidaMercanciaDetalleRepositorio : RepositorioGenericoEfCore<SalidaMercanciaDetalle, (int Entry, int NoLinea)>
    {
        public SalidaMercanciaDetalleRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // Clave primaria compuesta real (Entry + NoLinea): FindAsync necesita ambas partes, en el
        // mismo orden en que se declaró HasKey en ApiDbTestContext.OnModelCreating.
        public override async Task<SalidaMercanciaDetalle?> ObtenerAsync((int Entry, int NoLinea) id)
        {
            return await DbSet.FindAsync(id.Entry, id.NoLinea);
        }

        // El detalle muestra el código de la unidad de medida en cada línea.
        public override Task<IQueryable<SalidaMercanciaDetalle>> ObtenerTodoAsync()
        {
            return Task.FromResult(DbSet.Include(d => d.UomEntryNavigation).AsQueryable());
        }
    }
}
