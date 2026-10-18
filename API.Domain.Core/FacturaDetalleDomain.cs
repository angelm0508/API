using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class FacturaDetalleDomain : IFacturaDetalleDomain
    {
        private const string SubTipoFactura = "--";

        private readonly IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> _repoGenericoDet;
        private readonly IRepositorioGenerico<Factura, int> _repoEncabezado;

        public FacturaDetalleDomain(
            IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> repoGenericoDet,
            IRepositorioGenerico<Factura, int> repoEncabezado)
        {
            _repoGenericoDet = repoGenericoDet;
            _repoEncabezado = repoEncabezado;
        }

        #region async methods
        public async Task<int> InsertarAsync(FacturaDetalle obj)
        {
            // Las líneas se crean únicamente al registrar el documento (FacturaDomain.InsertarAsync).
            // Este endpoint suelto no debe crear líneas: sin FK a Factura, un Entry inexistente
            // generaría una línea huérfana.
            await Task.CompletedTask;
            throw new Exception("Las líneas se definen al crear el documento y no se pueden agregar después.");
        }

        public async Task<bool> ActualizarAsync(int entry, int noLinea, FacturaDetalle obj)
        {
            await LanzarSiElDocumentoExisteAsync(entry);
            return await _repoGenericoDet.ActualizarAsync((entry, noLinea), obj);
        }

        public async Task<bool> EliminarAsync(int entry, int noLinea)
        {
            await LanzarSiElDocumentoExisteAsync(entry);
            return await _repoGenericoDet.EliminarAsync((entry, noLinea));
        }

        public async Task<FacturaDetalle> ObtenerAsync(int entry, int noLinea)
        {
            // Las líneas de una nota de débito no se exponen por este recurso.
            var encabezado = await _repoEncabezado.ObtenerAsync(entry);
            if (encabezado is not null && encabezado.DocSubType != SubTipoFactura)
                return null;
            return await _repoGenericoDet.ObtenerAsync((entry, noLinea));
        }

        public async Task<IQueryable<FacturaDetalle>> ObtenerTodoAsync()
        {
            var lineas = await _repoGenericoDet.ObtenerTodoAsync();
            var entradasDeNotas = (await _repoEncabezado.ObtenerTodoAsync())
                .Where(h => h.DocSubType != SubTipoFactura)
                .Select(h => h.Entry);
            return lineas.Where(l => !entradasDeNotas.Contains(l.Entry));
        }

        public async Task<IEnumerable<FacturaDetalle>> ObtenerPorFacturaAsync(int entry)
        {
            var encabezado = await _repoEncabezado.ObtenerAsync(entry);
            if (encabezado is not null && encabezado.DocSubType != SubTipoFactura)
                return Array.Empty<FacturaDetalle>();

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
