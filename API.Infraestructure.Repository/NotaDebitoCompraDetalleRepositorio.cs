using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class NotaDebitoCompraDetalleRepositorio : RepositorioGenericoEfCore<NotaDebitoCompraDetalle, (int Entry, int NoLinea)>
    {
        public NotaDebitoCompraDetalleRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // Clave primaria compuesta real (Entry + NoLinea): FindAsync necesita ambas partes, en el
        // mismo orden en que se declaro HasKey en ApiDbTestContext.OnModelCreating.
        public override async Task<NotaDebitoCompraDetalle?> ObtenerAsync((int Entry, int NoLinea) id)
        {
            return await DbSet.FindAsync(id.Entry, id.NoLinea);
        }
    }
}
