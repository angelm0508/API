using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class FacturaCompraDetalleDomain : IFacturaCompraDetalleDomain
    {
        private const string SubTipoFactura = "--";

        private readonly IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> _repoGenericoDet;
        private readonly IRepositorioGenerico<FacturaCompra, int> _repoEncabezado;

        public FacturaCompraDetalleDomain(
            IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> repoGenericoDet,
            IRepositorioGenerico<FacturaCompra, int> repoEncabezado)
        {
            _repoGenericoDet = repoGenericoDet;
            _repoEncabezado = repoEncabezado;
        }

        #region async methods
        public async Task<int> InsertarAsync(FacturaCompraDetalle obj)
        {
            // Las líneas se crean únicamente al registrar el documento (FacturaCompraDomain.InsertarAsync).
            // Este endpoint suelto no debe crear líneas: sin FK a FacturaCompra, un Entry inexistente
            // generaría una línea huérfana.
            await Task.CompletedTask;
            throw new Exception("Las líneas se definen al crear el documento y no se pueden agregar después.");
        }

        public async Task<bool> ActualizarAsync(int entry, int noLinea, FacturaCompraDetalle obj)
        {
            await LanzarSiElDocumentoExisteAsync(entry);
            return await _repoGenericoDet.ActualizarAsync((entry, noLinea), obj);
        }

        public async Task<bool> EliminarAsync(int entry, int noLinea)
        {
            await LanzarSiElDocumentoExisteAsync(entry);
            return await _repoGenericoDet.EliminarAsync((entry, noLinea));
        }

        public async Task<FacturaCompraDetalle> ObtenerAsync(int entry, int noLinea)
        {
            // Las líneas de una nota de débito no se exponen por este recurso.
            var encabezado = await _repoEncabezado.ObtenerAsync(entry);
            if (encabezado is not null && encabezado.DocSubType != SubTipoFactura)
                return null;
            return await _repoGenericoDet.ObtenerAsync((entry, noLinea));
        }

        public async Task<IQueryable<FacturaCompraDetalle>> ObtenerTodoAsync()
        {
            var lineas = await _repoGenericoDet.ObtenerTodoAsync();
            var entradasDeNotas = (await _repoEncabezado.ObtenerTodoAsync())
                .Where(h => h.DocSubType != SubTipoFactura)
                .Select(h => h.Entry);
            return lineas.Where(l => !entradasDeNotas.Contains(l.Entry));
        }

        public async Task<IEnumerable<FacturaCompraDetalle>> ObtenerPorFacturaCompraAsync(int entry)
        {
            var encabezado = await _repoEncabezado.ObtenerAsync(entry);
            if (encabezado is not null && encabezado.DocSubType != SubTipoFactura)
                return Array.Empty<FacturaCompraDetalle>();

            var queryable = await _repoGenericoDet.ObtenerTodoAsync();
            return await queryable.Where(x => x.Entry == entry).ToListAsync();
        }

        private async Task LanzarSiElDocumentoExisteAsync(int entry)
        {
            if (await _repoEncabezado.ObtenerAsync(entry) is not null)
                throw new Exception("Las líneas se definen al crear el documento y no se pueden modificar después.");
        }
        #endregion
    }
}
