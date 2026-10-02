using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class PlantillaPermisoDetalleRepositorio : RepositorioGenericoEfCore<PlantillaPermisoDetalle, (int PlantillaId, string CodigoModulo)>
    {
        public PlantillaPermisoDetalleRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        public override async Task<PlantillaPermisoDetalle?> ObtenerAsync((int PlantillaId, string CodigoModulo) id)
        {
            return await DbSet.FindAsync(id.PlantillaId, id.CodigoModulo);
        }
    }
}
