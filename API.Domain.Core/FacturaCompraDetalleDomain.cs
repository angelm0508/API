using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class FacturaCompraDetalleDomain : IFacturaCompraDetalleDomain
    {
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
            await LanzarSiElDocumentoExisteAsync(obj.Entry);

            var lineasExistentes = await ObtenerPorFacturaCompraAsync(obj.Entry);
            obj.NoLinea = lineasExistentes.Any() ? lineasExistentes.Max(x => x.NoLinea) + 1 : 1;

            var insertado = await _repoGenericoDet.InsertarAsync(obj);
            return insertado.NoLinea;
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
            return await _repoGenericoDet.ObtenerAsync((entry, noLinea));
        }

        public async Task<IQueryable<FacturaCompraDetalle>> ObtenerTodoAsync()
        {
            return await _repoGenericoDet.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<FacturaCompraDetalle>> ObtenerPorFacturaCompraAsync(int entry)
        {
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
