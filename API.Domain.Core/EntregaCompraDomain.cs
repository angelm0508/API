using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class EntregaCompraDomain : IEntregaCompraDomain
    {
        // CHECK constraint de la tabla: TipoObjeto='12'. Se fuerza siempre en el servidor.
        private const string TipoObjetoEntregaCompra = "12";

        private readonly IRepositorioGenerico<EntregaCompra, int> _repoEntregaCompra;
        private readonly IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)> _repoDetalle;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoNumeracion;
        private readonly IEjecutorTransaccion _tx;
        private readonly IInventarioAsientoService _asiento;

        public EntregaCompraDomain(
            IRepositorioGenerico<EntregaCompra, int> repoEntregaCompra,
            IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)> repoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoNumeracion,
            IEjecutorTransaccion tx,
            IInventarioAsientoService asiento)
        {
            _repoEntregaCompra = repoEntregaCompra;
            _repoDetalle = repoDetalle;
            _repoNumeracion = repoNumeracion;
            _tx = tx;
            _asiento = asiento;
        }

        #region async methods
        public async Task<int> InsertarAsync(EntregaCompra obj, IEnumerable<EntregaCompraDetalle> lineas)
        {
            obj.TipoObjeto = TipoObjetoEntregaCompra;
            obj.EstadoInv = "A";

            var serie = await _repoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.Bloqueado == "S")
                throw new Exception("La serie está bloqueada y no se puede usar para registrar entregas de compra.");

            if (serie.Manual == "S")
            {
                if (obj.NumDoc <= 0)
                    throw new Exception("El número de documento es requerido para series manuales.");
            }
            else
            {
                if (serie.SigNumero == null)
                    throw new Exception("La serie no tiene configurado el número siguiente.");
                if (serie.FinNumero.HasValue && serie.SigNumero.Value > serie.FinNumero.Value)
                    throw new Exception("Se agotó la numeración disponible en esta serie.");

                obj.NumDoc = serie.SigNumero.Value;
                serie.SigNumero = serie.SigNumero.Value + 1; // rastreada por el mismo contexto; se persiste en el Save de la transacción
            }

            var lineasList = lineas?.ToList() ?? new List<EntregaCompraDetalle>();

            return await _tx.EjecutarAsync(async () =>
            {
                await _repoEntregaCompra.InsertarAsync(obj); // Save #1: asigna obj.Entry

                var noLinea = 1;
                foreach (var linea in lineasList)
                {
                    linea.Entry = obj.Entry;
                    linea.NoLinea = noLinea++;
                    await _repoDetalle.AgregarSinGuardarAsync(linea);
                }

                var movimientos = lineasList
                    .Where(l => (l.Cantidad ?? 0m) > 0m)
                    .Select(l => new MovimientoRequest(
                        TipoDoc: TipoObjetoEntregaCompra,
                        DocEntry: obj.Entry,
                        DocLinea: l.NoLinea,
                        CodArticulo: l.CodArticulo!,
                        CodAlmacen: l.CodAlmacen!,
                        Cantidad: l.Cantidad!.Value,
                        PrecioUnitario: l.Precio ?? 0m,
                        Fecha: obj.FechaDoc ?? DateTime.Now))
                    .ToList();

                await _asiento.AsentarAsync(movimientos);

                return obj.Entry;
            });
            // EjecutarAsync: Save #2 (líneas + inventario) + Commit
        }

        public async Task<bool> ActualizarAsync(int id, EntregaCompra obj)
        {
            var existente = await _repoEntregaCompra.ObtenerAsync(id);
            if (existente is null)
                return false;

            if (existente.Cancelado == "S")
                throw new Exception("El documento está cancelado y no se puede modificar.");

            // Cancelación: Cancelado pasa a 'S'.
            if (obj.Cancelado == "S")
            {
                return await _tx.EjecutarAsync(async () =>
                {
                    await _asiento.RevertirAsync(TipoObjetoEntregaCompra, id);
                    existente.Cancelado = "S";
                    existente.EstadoInv = "C";
                    existente.FechaCancelado = DateTime.Now;
                    existente.Comentario = obj.Comentario;
                    return true;
                });
            }

            // Edición inocua: solo el comentario.
            return await _tx.EjecutarAsync(async () =>
            {
                existente.Comentario = obj.Comentario;
                return true;
            });
        }

        public async Task<bool> EliminarAsync(int id)
        {
            var existente = await _repoEntregaCompra.ObtenerAsync(id);
            if (existente is null)
                return false;

            if (existente.EstadoInv == "A" && existente.Cancelado != "S")
                throw new Exception("Cancele el documento (Cancelado='S') antes de eliminarlo.");

            // Documento cancelado (inventario ya revertido) o sin asiento: borrar líneas y encabezado.
            var detalles = await _repoDetalle.ObtenerTodoAsync();
            var lineas = await detalles.Where(d => d.Entry == id).ToListAsync();
            foreach (var linea in lineas)
                await _repoDetalle.EliminarAsync((linea.Entry, linea.NoLinea));

            return await _repoEntregaCompra.EliminarAsync(id);
        }

        public async Task<EntregaCompra> ObtenerAsync(int id)
        {
            return await _repoEntregaCompra.ObtenerAsync(id);
        }

        public async Task<IQueryable<EntregaCompra>> ObtenerTodoAsync()
        {
            return await _repoEntregaCompra.ObtenerTodoAsync();
        }
        #endregion
    }
}
