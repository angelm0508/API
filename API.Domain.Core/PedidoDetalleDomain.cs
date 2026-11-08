using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class PedidoDetalleDomain : IPedidoDetalleDomain
    {
        private readonly IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)> _repoGenericoDet;

        private readonly IDocumentoBaseService _documentoBase;
        private readonly IUnidadMedidaService? _unidades;

        public PedidoDetalleDomain(
            IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)> repoGenericoDet,
            IDocumentoBaseService documentoBase,
            IUnidadMedidaService? unidades = null)
        {
            _repoGenericoDet = repoGenericoDet;
            _documentoBase = documentoBase;
            _unidades = unidades;
        }

        #region async methods
        public async Task<int> InsertarAsync(PedidoDetalle obj)
        {
            // Una línea suelta no puede traer documento base: la referencia (y la validación de cantidad abierta) se define
            // únicamente al crear el documento con sus líneas (PedidoDomain.InsertarAsync(Pedido, líneas)).
            if (obj.BaseTipo > 0)
                throw new Exception("Las líneas copiadas se definen al crear el documento.");

            var (uomEntry, factor, cantBase) = await UnidadesLinea.ResolverLineaAsync(_unidades, obj.CodArticulo, obj.UomEntry, obj.Cantidad);
            obj.UomEntry = uomEntry;
            obj.FactorUom = factor;
            obj.CantidadBase = cantBase;

            var lineasExistentes = await ObtenerPorPedidoAsync(obj.Entry);
            obj.NoLinea = lineasExistentes.Any() ? lineasExistentes.Max(x => x.NoLinea) + 1 : 1;

            var insertado = await _repoGenericoDet.InsertarAsync(obj);
            return insertado.NoLinea;
        }

        public async Task<bool> ActualizarAsync(int entry, int noLinea, PedidoDetalle obj)
        {
            await _documentoBase.ValidarLineaModificableAsync(TipoDocumentoVenta.Pedido, entry, noLinea, "modificar");

            // La referencia al documento base es inmutable: se toma siempre de la línea guardada, nunca de lo que llegue
            // (el panel de edición de la Web no envía estos campos y el repositorio genérico copia todas las columnas,
            // así que sin esto una edición normal borraría la referencia o permitiría fabricarla).
            var guardada = await _repoGenericoDet.ObtenerAsync((entry, noLinea));
            if (guardada is not null)
            {
                if (guardada.BaseTipo > 0)
                {
                    if (!string.Equals(obj.CodArticulo, guardada.CodArticulo, StringComparison.Ordinal))
                        throw new Exception("El artículo de una línea copiada no se puede cambiar.");
                    if (obj.UomEntry.HasValue && guardada.UomEntry.HasValue && obj.UomEntry != guardada.UomEntry)
                        throw new Exception("La unidad de medida de una línea copiada no se puede cambiar.");
                    if ((obj.Cantidad ?? 0m) > (guardada.Cantidad ?? 0m))
                        throw new Exception("La cantidad de una línea copiada no puede aumentar. Cree otro documento con lo que falta.");
                    obj.UomEntry ??= guardada.UomEntry;
                }

                obj.BaseTipo = guardada.BaseTipo;
                obj.BaseEntry = guardada.BaseEntry;
                obj.BaseLinea = guardada.BaseLinea;
                obj.TipoDocDestino = guardada.TipoDocDestino;
                obj.DocDestinoEntry = guardada.DocDestinoEntry;
                obj.BaseRef = guardada.BaseRef;
            }

            var (uomEntry, factor, cantBase) = await UnidadesLinea.ResolverLineaAsync(_unidades, obj.CodArticulo, obj.UomEntry, obj.Cantidad);
            obj.UomEntry = uomEntry;
            obj.FactorUom = factor;
            obj.CantidadBase = cantBase;

            return await _repoGenericoDet.ActualizarAsync((entry, noLinea), obj);
        }

        public async Task<bool> EliminarAsync(int entry, int noLinea)
        {
            await _documentoBase.ValidarLineaModificableAsync(TipoDocumentoVenta.Pedido, entry, noLinea, "eliminar");
            return await _repoGenericoDet.EliminarAsync((entry, noLinea));
        }

        public async Task<PedidoDetalle> ObtenerAsync(int entry, int noLinea)
        {
            return await _repoGenericoDet.ObtenerAsync((entry, noLinea));
        }

        public async Task<IQueryable<PedidoDetalle>> ObtenerTodoAsync()
        {
            return await _repoGenericoDet.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<PedidoDetalle>> ObtenerPorPedidoAsync(int entry)
        {
            var queryable = await _repoGenericoDet.ObtenerTodoAsync();
            return await queryable.Where(x => x.Entry == entry).ToListAsync();
        }
        #endregion
    }
}
