using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class ExistenciaArticuloRepositorio : RepositorioGenericoEfCore<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)>
    {
        public ExistenciaArticuloRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // Clave primaria compuesta (CodArticulo + CodAlmacen), en el mismo orden del HasKey.
        public override async Task<ExistenciaArticulo?> ObtenerAsync((string CodArticulo, string CodAlmacen) id)
        {
            return await DbSet.FindAsync(id.CodArticulo, id.CodAlmacen);
        }
    }
}
