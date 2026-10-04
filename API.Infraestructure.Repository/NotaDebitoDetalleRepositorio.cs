using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class NotaDebitoDetalleRepositorio : RepositorioGenericoEfCore<NotaDebitoDetalle, (int Entry, int NoLinea)>
    {
        public NotaDebitoDetalleRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // Clave primaria compuesta real (Entry + NoLinea): FindAsync necesita ambas partes, en el
        // mismo orden en que se declaro HasKey en ApiDbTestContext.OnModelCreating.
        public override async Task<NotaDebitoDetalle?> ObtenerAsync((int Entry, int NoLinea) id)
        {
            return await DbSet.FindAsync(id.Entry, id.NoLinea);
        }
    }
}
