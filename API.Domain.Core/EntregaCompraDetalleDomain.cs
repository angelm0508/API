using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class EntregaCompraDetalleDomain : IEntregaCompraDetalleDomain
    {
        private readonly IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)> _repoGenericoDet;
        private readonly IRepositorioGenerico<EntregaCompra, int> _repoEncabezado;

        public EntregaCompraDetalleDomain(
            IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)> repoGenericoDet,
            IRepositorioGenerico<EntregaCompra, int> repoEncabezado)
        {
            _repoGenericoDet = repoGenericoDet;
            _repoEncabezado = repoEncabezado;
        }

        #region async methods
        public async Task<int> InsertarAsync(EntregaCompraDetalle obj)
        {
            // Las líneas se crean únicamente al registrar el documento (EntregaCompraDomain.InsertarAsync).
            // Este endpoint suelto no debe crear líneas: sin FK a EntregaCompra, un Entry inexistente
            // generaría una línea huérfana.
            await Task.CompletedTask;
            throw new Exception("Las líneas se definen al crear el documento y no se pueden agregar después.");
        }

        public async Task<bool> ActualizarAsync(int entry, int noLinea, EntregaCompraDetalle obj)
        {
            await LanzarSiElDocumentoExisteAsync(entry);
            return await _repoGenericoDet.ActualizarAsync((entry, noLinea), obj);
        }

        public async Task<bool> EliminarAsync(int entry, int noLinea)
        {
            await LanzarSiElDocumentoExisteAsync(entry);
            return await _repoGenericoDet.EliminarAsync((entry, noLinea));
        }

        public async Task<EntregaCompraDetalle> ObtenerAsync(int entry, int noLinea)
        {
            return await _repoGenericoDet.ObtenerAsync((entry, noLinea));
        }

        public async Task<IQueryable<EntregaCompraDetalle>> ObtenerTodoAsync()
        {
            return await _repoGenericoDet.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<EntregaCompraDetalle>> ObtenerPorEntregaCompraAsync(int entry)
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
