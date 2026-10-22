using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class FacturaDomain : IFacturaDomain
    {
        // CHECK constraint de la tabla: TipoObjeto='6'. Se fuerza siempre en el servidor.
        private const string TipoObjetoFactura = "6";
        private const string SubTipoFactura = "--";

        private readonly IRepositorioGenerico<Factura, int> _repoFactura;
        private readonly IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> _repoDetalle;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoNumeracion;
        private readonly IEjecutorTransaccion _tx;
        private readonly IInventarioAsientoService _asiento;
        private readonly IDocumentoBaseService _documentoBase;

        public FacturaDomain(
            IRepositorioGenerico<Factura, int> repoFactura,
            IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> repoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoNumeracion,
            IEjecutorTransaccion tx,
            IInventarioAsientoService asiento,
            IDocumentoBaseService documentoBase)
        {
            _repoFactura = repoFactura;
            _repoDetalle = repoDetalle;
            _repoNumeracion = repoNumeracion;
            _tx = tx;
            _asiento = asiento;
            _documentoBase = documentoBase;
        }

        #region async methods
        public async Task<int> InsertarAsync(Factura obj, IEnumerable<FacturaDetalle> lineas)
        {
            obj.TipoObjeto = TipoObjetoFactura;
            obj.DocSubType = SubTipoFactura;
            obj.DocType = "I";
            obj.EstadoInv = "A";
            obj.Cancelado = "N";                  // un documento recién registrado nunca nace cancelado
            obj.FechaCancelado = null;

            // CK_Factura_Base: una factura ('--') solo admite BaseTipo = -1 en la cabecera. La referencia al documento
            // base (copiar a) vive únicamente en las líneas.
            obj.BaseTipo = -1;
            obj.BaseEntry = null;

            var serie = await _repoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.CodigoObj != TipoObjetoFactura || serie.SubTipoDoc != SubTipoFactura)
                throw new Exception("La serie no corresponde a facturas.");

            if (serie.Bloqueado == "S")
                throw new Exception("La serie está bloqueada y no se puede usar para registrar facturas.");

            var lineasList = lineas?.ToList() ?? new List<FacturaDetalle>();
            ValidarLineas(lineasList); // antes de numerar: un error de línea no debe avanzar el consecutivo

            // Líneas copiadas de una cotización, un pedido o una entrega: se validan antes de numerar, así un error no
            // avanza el consecutivo (ver DocumentoBaseService.ValidarLineasAsync).
            var basadas = LineasBase.Extraer(lineasList.Select(l => (l.BaseTipo, l.BaseEntry, l.BaseLinea, l.CodArticulo, l.Cantidad)));
            if (basadas.Count > 0)
                await _documentoBase.ValidarLineasAsync(TipoDocumentoVenta.Factura, obj.CodigoSn, basadas);

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

            return await _tx.EjecutarAsync(async () =>
            {
                await _repoFactura.InsertarAsync(obj); // Save #1: asigna obj.Entry

                var noLinea = 1;
                foreach (var linea in lineasList)
                {
                    linea.Entry = obj.Entry;
                    linea.NoLinea = noLinea++;
                    await _repoDetalle.AgregarSinGuardarAsync(linea);
                }

                var movimientos = lineasList
                    .Where(l => (l.Cantidad ?? 0m) > 0m
                        && l.BaseTipo != TipoDocumentoVenta.Entrega            // BaseTipo 5: esa mercancía ya la movió su Entrega
                        && l.BaseTipo != TipoDocumentoVenta.OrdenServicio)     // BaseTipo 70: los insumos ya salieron al agregarse a la orden; con base Cotización o Pedido nadie entregó todavía
                    .Select(l => new MovimientoRequest(
                        TipoDoc: TipoObjetoFactura,
                        DocEntry: obj.Entry,
                        DocLinea: l.NoLinea,
                        CodArticulo: l.CodArticulo!,
                        CodAlmacen: l.CodAlmacen!,
                        Cantidad: -(l.Cantidad!.Value),   // negativo = salida de stock
                        PrecioUnitario: l.Precio ?? 0m,
                        Fecha: obj.FechaDoc ?? DateTime.Now))
                    .ToList();

                await _asiento.AsentarAsync(movimientos);

                return obj.Entry;
            });
            // EjecutarAsync: Save #2 (líneas + inventario) + Commit
        }

        public async Task<bool> ActualizarAsync(int id, Factura obj)
        {
            var existente = await ObtenerFacturaAsync(id);
            if (existente is null)
                return false;

            if (existente.Cancelado == "S")
                throw new Exception("El documento está cancelado y no se puede modificar.");

            // Cancelación: Cancelado pasa a 'S'.
            if (obj.Cancelado == "S")
            {
                return await _tx.EjecutarAsync(async () =>
                {
                    await _asiento.RevertirAsync(TipoObjetoFactura, id);
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
            var existente = await ObtenerFacturaAsync(id);
            if (existente is null)
                return false;

            if (existente.EstadoInv == "A" && existente.Cancelado != "S")
                throw new Exception("Cancele el documento (Cancelado='S') antes de eliminarlo.");

            // Documento cancelado (inventario ya revertido) o sin asiento: borrar líneas y encabezado.
            return await _tx.EjecutarAsync(async () =>
            {
                var detalles = await _repoDetalle.ObtenerTodoAsync();
                var lineas = await detalles.Where(d => d.Entry == id).ToListAsync();
                foreach (var linea in lineas)
                    await _repoDetalle.EliminarAsync((linea.Entry, linea.NoLinea));

                return await _repoFactura.EliminarAsync(id);
            });
        }

        public async Task<Factura> ObtenerAsync(int id)
        {
            return await ObtenerFacturaAsync(id);
        }

        public async Task<IQueryable<Factura>> ObtenerTodoAsync()
        {
            var facturas = await _repoFactura.ObtenerTodoAsync();
            return facturas.Where(f => f.DocSubType == SubTipoFactura);
        }
        #endregion

        #region validaciones
        // Una fila de Factura con DocSubType distinto de '--' es una nota de débito: su API es otra.
        private async Task<Factura?> ObtenerFacturaAsync(int id)
        {
            var factura = await _repoFactura.ObtenerAsync(id);
            return factura is not null && factura.DocSubType == SubTipoFactura ? factura : null;
        }

        private static void ValidarLineas(IList<FacturaDetalle> lineas)
        {
            for (var i = 0; i < lineas.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(lineas[i].CodArticulo))
                    throw new Exception($"Línea {i + 1}: el artículo es requerido.");
                if (string.IsNullOrWhiteSpace(lineas[i].CodAlmacen))
                    throw new Exception($"Línea {i + 1}: el almacén es requerido.");
            }
        }
        #endregion
    }
}
