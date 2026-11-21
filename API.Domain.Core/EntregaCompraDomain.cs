using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Domain.Interface.Existencias;
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
        private readonly IUnidadMedidaService? _unidades;
        private readonly IExistenciaCompromisoService _compromisos;
        private readonly IDocumentoBaseCompraService _documentoBase;

        public EntregaCompraDomain(
            IRepositorioGenerico<EntregaCompra, int> repoEntregaCompra,
            IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)> repoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoNumeracion,
            IEjecutorTransaccion tx,
            IInventarioAsientoService asiento,
            IUnidadMedidaService? unidades,
            IExistenciaCompromisoService compromisos,
            IDocumentoBaseCompraService documentoBase)
        {
            _repoEntregaCompra = repoEntregaCompra;
            _repoDetalle = repoDetalle;
            _repoNumeracion = repoNumeracion;
            _tx = tx;
            _asiento = asiento;
            _unidades = unidades;
            _compromisos = compromisos;
            _documentoBase = documentoBase;
        }

        #region async methods
        public async Task<int> InsertarAsync(EntregaCompra obj, IEnumerable<EntregaCompraDetalle> lineas)
        {
            // NIT copiado del socio: se recorta y se valida antes de numerar (un rechazo no consume consecutivo).
            obj.Nit = NitDocumento.Normalizar(obj.Nit);
            obj.TipoObjeto = TipoObjetoEntregaCompra;
            obj.EstadoInv = "A";

            // CK de la cabecera: la referencia al documento base vive únicamente en las líneas.
            obj.BaseTipo = -1;
            obj.BaseEntry = null;

            var serie = await _repoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.Bloqueado == "S")
                throw new Exception("La serie está bloqueada y no se puede usar para registrar entregas de compra.");

            var lineasList = lineas?.ToList() ?? new List<EntregaCompraDetalle>();

            // Líneas copiadas de un pedido de compra: se validan antes de numerar, así un error no avanza el consecutivo
            // (ver DocumentoBaseCompraService.ValidarLineasAsync).
            var basadas = LineasBase.Extraer(lineasList.Select(l => (l.BaseTipo, l.BaseEntry, l.BaseLinea, l.CodArticulo, l.Cantidad)));
            if (basadas.Count > 0)
                await _documentoBase.ValidarLineasAsync(TipoDocumentoCompra.EntregaCompra, obj.CodigoSn, basadas);

            foreach (var linea in lineasList)
            {
                if ((linea.Cantidad ?? 0m) <= 0m) continue;
                var (uomEntry, factor, cantBase) = await UnidadesLinea.ResolverLineaAsync(_unidades, linea.CodArticulo, linea.UomEntry, linea.Cantidad);
                linea.UomEntry = uomEntry;
                linea.FactorUom = factor;
                linea.CantidadBase = cantBase;
            }

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

            var entry = await _tx.EjecutarAsync(async () =>
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
                        Cantidad: l.CantidadBase ?? l.Cantidad!.Value,
                        PrecioUnitario: UnidadesLinea.PrecioBase(l.Precio ?? 0m, l.FactorUom),
                        Fecha: obj.FechaDoc ?? DateTime.Now))
                    .ToList();

                await _asiento.AsentarAsync(movimientos);

                return obj.Entry;
            });
            // EjecutarAsync: Save #2 (líneas + inventario) + Commit

            // Pedido de existencias: solo con la entrega ya confirmada y como último paso (el aviso nunca lanza). Este dominio
            // no corre anidado en ninguna transacción exterior; sin líneas de pedido de compra base el servicio retorna sin
            // hacer nada.
            await _compromisos.NotificarDocumentoAsync(TipoDocCompromiso.EntregaCompra, entry);
            return entry;
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
                var cancelada = await _tx.EjecutarAsync(async () =>
                {
                    await _asiento.RevertirAsync(TipoObjetoEntregaCompra, id);
                    existente.Cancelado = "S";
                    existente.EstadoInv = "C";
                    existente.FechaCancelado = DateTime.Now;
                    // Solo se toca el comentario si el cliente lo envió: el botón "Cancelar
                    // documento" del Web manda únicamente { Cancelado: 'S' }, y no debe borrar la
                    // nota existente (igual que SAP B1: anular no vacía las observaciones).
                    if (obj.Comentario != null)
                        existente.Comentario = obj.Comentario;
                    return true;
                });

                // Pedido de existencias: lo que la entrega consumía del pedido de compra vuelve a quedar abierto. Tras confirmar
                // y como último paso (el aviso nunca lanza). Una entrega cancelada no se reabre, y editar el comentario no
                // cambia nada.
                if (cancelada)
                    await _compromisos.NotificarDocumentoAsync(TipoDocCompromiso.EntregaCompra, id);
                return cancelada;
            }

            // Edición inocua: solo el comentario (replace-semantics: enviar vacío lo borra).
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

            // Pedido de existencias: los pares artículo/almacén se leen ANTES de borrar (después las líneas ya no existen).
            var pares = await _compromisos.ObtenerParesAsync(TipoDocCompromiso.EntregaCompra, id);

            // Documento cancelado (inventario ya revertido) o sin asiento: borrar líneas y encabezado.
            var eliminada = await _tx.EjecutarAsync(async () =>
            {
                var detalles = await _repoDetalle.ObtenerTodoAsync();
                var lineas = await detalles.Where(d => d.Entry == id).ToListAsync();
                foreach (var linea in lineas)
                    await _repoDetalle.EliminarAsync((linea.Entry, linea.NoLinea));

                return await _repoEntregaCompra.EliminarAsync(id);
            });

            if (eliminada)
                await _compromisos.NotificarParesAsync(pares);
            return eliminada;
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
