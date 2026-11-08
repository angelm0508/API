using API.Domain.Core.Inventario;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class UnidadMedidaService : IUnidadMedidaService
    {
        // Grupo y unidad "Manual" (semilla bloqueada): equivalen a "sin unidad".
        private const int GrupoManual = -1;

        private readonly IRepositorioGenerico<Articulo, string> _repoArticulo;
        private readonly IRepositorioGenerico<GrupoUnidadMedidaArticulo, int> _repoGrupo;
        private readonly IRepositorioGenerico<GrupoUnidadMedidaDetArticulo, (int GrpMedidaEntry, int NumLinea)> _repoGrupoDet;
        private readonly IRepositorioGenerico<UnidadMedidaArticulo, int> _repoUnidad;
        private readonly IRepositorioGenerico<OrdenServicioDetalle, (int Entry, int NoLinea)>? _repoOrdenDet;

        public UnidadMedidaService(
            IRepositorioGenerico<Articulo, string> repoArticulo,
            IRepositorioGenerico<GrupoUnidadMedidaArticulo, int> repoGrupo,
            IRepositorioGenerico<GrupoUnidadMedidaDetArticulo, (int GrpMedidaEntry, int NumLinea)> repoGrupoDet,
            IRepositorioGenerico<UnidadMedidaArticulo, int> repoUnidad,
            IRepositorioGenerico<OrdenServicioDetalle, (int Entry, int NoLinea)>? repoOrdenDet = null)
        {
            _repoArticulo = repoArticulo;
            _repoGrupo = repoGrupo;
            _repoGrupoDet = repoGrupoDet;
            _repoUnidad = repoUnidad;
            _repoOrdenDet = repoOrdenDet;
        }

        public async Task<UnidadResuelta> ResolverAsync(string codArticulo, int? uomEntry)
        {
            var unidades = await ObtenerUnidadesAsync(codArticulo);

            if (unidades.Unidades.Count == 0)
            {
                if (uomEntry is not null && uomEntry != GrupoManual)
                    throw new Exception($"El artículo {codArticulo} no tiene grupo de unidades de medida: no admite unidad en la línea.");
                return new UnidadResuelta(null, null, 1m);
            }

            var elegida = uomEntry is null
                ? unidades.Unidades.First(u => u.EsBase)
                : unidades.Unidades.FirstOrDefault(u => u.UomEntry == uomEntry)
                    ?? throw new Exception($"La unidad de medida {uomEntry} no pertenece al grupo de medida del artículo {codArticulo}.");

            return new UnidadResuelta(elegida.UomEntry, elegida.Codigo, elegida.Factor);
        }

        public async Task<UnidadesArticulo> ObtenerUnidadesAsync(string codArticulo)
        {
            var articulo = await _repoArticulo.ObtenerAsync(codArticulo)
                ?? throw new ArticuloNoExisteException(codArticulo);

            var grupoEntry = articulo.CodigoGrpUnidadMedida;
            if (grupoEntry is null || grupoEntry == GrupoManual)
                return new UnidadesArticulo(codArticulo, null, Array.Empty<UnidadArticulo>());

            var grupo = await _repoGrupo.ObtenerAsync(grupoEntry.Value)
                ?? throw new Exception($"El grupo de unidades de medida {grupoEntry} del artículo {codArticulo} no existe.");

            var detalles = await (await _repoGrupoDet.ObtenerTodoAsync())
                .Where(d => d.GrpMedidaEntry == grupo.Entry)
                .ToListAsync();
            var catalogo = await (await _repoUnidad.ObtenerTodoAsync()).ToListAsync();

            string Codigo(int entry) => catalogo.FirstOrDefault(u => u.Entry == entry)?.Codigo ?? entry.ToString();
            string? Nombre(int entry) => catalogo.FirstOrDefault(u => u.Entry == entry)?.Nombre;

            var lista = new List<UnidadArticulo>
            {
                new(grupo.BaseMedida, Codigo(grupo.BaseMedida), Nombre(grupo.BaseMedida), 1m, true)
            };

            foreach (var d in detalles.Where(d => d.Activo != "N" && d.MedidaEntry != grupo.BaseMedida).OrderBy(d => d.NumLinea))
            {
                if (lista.Any(u => u.UomEntry == d.MedidaEntry))
                    continue;
                if ((d.CantAlternativa ?? 0m) <= 0m || (d.CantBase ?? 0m) <= 0m)
                    throw new Exception($"La unidad {Codigo(d.MedidaEntry)} del grupo de medida del artículo {codArticulo} no tiene una equivalencia válida.");

                lista.Add(new UnidadArticulo(d.MedidaEntry, Codigo(d.MedidaEntry), Nombre(d.MedidaEntry), d.CantBase!.Value / d.CantAlternativa!.Value, false));
            }

            return new UnidadesArticulo(codArticulo, grupo.Entry, lista);
        }

        public async Task ValidarCambioGrupoAsync(string codArticulo, int? nuevoGrupoEntry)
        {
            var actual = await _repoArticulo.ObtenerAsync(codArticulo);
            if (actual is null)
                return;

            var grupoActual = actual.CodigoGrpUnidadMedida == GrupoManual ? null : actual.CodigoGrpUnidadMedida;
            var grupoNuevo = nuevoGrupoEntry == GrupoManual ? null : nuevoGrupoEntry;
            if (grupoActual == grupoNuevo)
                return;

            var query = await _repoArticulo.ObtenerTodoAsync();
            var enUso = await query.AnyAsync(a => a.Codigo == codArticulo && (
                a.MovimientoInventarios.Any() ||
                a.CotizacionDetalles.Any() ||
                a.PedidoDetalles.Any() ||
                a.EntregaDetalles.Any() ||
                a.FacturaDetalles.Any() ||
                a.PedidoCompraDetalles.Any() ||
                a.EntregaCompraDetalles.Any() ||
                a.EntradaMercanciaDetalles.Any() ||
                a.SalidaMercanciaDetalles.Any() ||
                a.FacturaCompraDetalles.Any()));

            if (!enUso && _repoOrdenDet is not null)
            {
                var ordenes = await _repoOrdenDet.ObtenerTodoAsync();
                enUso = await ordenes.AnyAsync(d => d.CodArticulo == codArticulo);
            }

            if (enUso)
                throw new Exception("No se puede cambiar el grupo de unidad de medida del artículo porque ya tiene movimientos o documentos registrados.");
        }
    }
}
