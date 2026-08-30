using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class PedidoCompraDetalleRepositorio : RepositorioGenericoEfCore<PedidoCompraDetalle, (int Entry, int NoLinea)>
    {
        public PedidoCompraDetalleRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // Clave primaria compuesta real (Entry + NoLinea): FindAsync necesita ambas partes, en el
        // mismo orden en que se declaró HasKey en ApiDbTestContext.OnModelCreating.
        public override async Task<PedidoCompraDetalle?> ObtenerAsync((int Entry, int NoLinea) id)
        {
            return await DbSet.FindAsync(id.Entry, id.NoLinea);
        }
    }
}
