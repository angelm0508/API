using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class ArticuloAlmacenRepositorio : RepositorioGenericoEfCore<ArticuloAlmacen, (string CodArticulo, string CodAlmacen)>
    {
        public ArticuloAlmacenRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // Clave primaria compuesta (CodArticulo + CodAlmacen), en el mismo orden del HasKey.
        public override async Task<ArticuloAlmacen?> ObtenerAsync((string CodArticulo, string CodAlmacen) id)
        {
            return await DbSet.FindAsync(id.CodArticulo, id.CodAlmacen);
        }
    }
}
