using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class ArticuloRepositorio : RepositorioGenericoEfCore<Articulo, string>
    {
        public ArticuloRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // Estas 4 columnas de valuación las gobierna el servicio de asiento de inventario, nunca el
        // CRUD de artículos (la pantalla de Artículos de INV-1 no las expone). ArticuloActualizarDTO
        // no las trae, así que _mapper.Map<Articulo>(dto) produce MetodoValuacion = null y costos = 0;
        // sin este blindaje ActualizarAsync los escribiría sobre columnas NOT NULL y rompería el PUT.
        protected override ISet<string> PropiedadesNoActualizables { get; } = new HashSet<string>
        {
            nameof(Articulo.MetodoValuacion),
            nameof(Articulo.CostoPromedio),
            nameof(Articulo.CostoEstandar),
            nameof(Articulo.ValorInventario),
        };

        public override async Task<Articulo?> ObtenerAsync(string id)
        {
            return await DbSet
                .Include(x => x.CodigoGrupoNavigation)
                .Include(x => x.CodigoGrpUnidadMedidaNavigation)
                .Include(x => x.FabricanteEntryNavigation)
                .FirstOrDefaultAsync(x => x.Codigo == id);
        }
    }
}
