using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class PedidoDomain : IPedidoDomain
    {
        // Código de objeto/documento reservado para Pedidos -- exigido por el CHECK constraint
        // de la tabla (TipoObjeto='4'). Se fuerza siempre en el servidor, sin confiar en lo que
        // envíe el cliente.
        private const string TipoObjetoPedido = "4";

        private readonly IRepositorioGenerico<Pedido, int> _repoGenericoPedido;
        private readonly IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)> _repoGenericoDetalle;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoGenericoNumeracion;
        private readonly IEjecutorTransaccion _tx;
        private readonly IDocumentoBaseService _documentoBase;

        public PedidoDomain(
            IRepositorioGenerico<Pedido, int> repoGenericoPedido,
            IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)> repoGenericoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoGenericoNumeracion,
            IEjecutorTransaccion tx,
            IDocumentoBaseService documentoBase)
        {
            _repoGenericoPedido = repoGenericoPedido;
            _repoGenericoDetalle = repoGenericoDetalle;
            _repoGenericoNumeracion = repoGenericoNumeracion;
            _tx = tx;
            _documentoBase = documentoBase;
        }

        #region async methods
        public async Task<int> InsertarAsync(Pedido obj)
        {
            obj.TipoObjeto = TipoObjetoPedido;
            await AsignarNumeroAsync(obj);

            var creado = await _repoGenericoPedido.InsertarAsync(obj);
            return creado.Entry;
        }

        public async Task<int> InsertarAsync(Pedido obj, IEnumerable<PedidoDetalle> lineas)
        {
            obj.TipoObjeto = TipoObjetoPedido;
            // CK de la cabecera: solo BaseTipo = -1. La referencia al documento base vive únicamente en las líneas.
            obj.BaseTipo = -1;
            obj.BaseEntry = null;

            var lineasList = lineas?.ToList() ?? new List<PedidoDetalle>();

            // Líneas copiadas de una cotización: se validan antes de numerar, así un error no avanza el consecutivo.
            var basadas = LineasBase.Extraer(lineasList.Select(l => (l.BaseTipo, l.BaseEntry, l.BaseLinea, l.CodArticulo, l.Cantidad)));
            if (basadas.Count > 0)
                await _documentoBase.ValidarLineasAsync(TipoDocumentoVenta.Pedido, obj.CodigoSn, basadas);

            await AsignarNumeroAsync(obj);

            return await _tx.EjecutarAsync(async () =>
            {
                var creado = await _repoGenericoPedido.InsertarAsync(obj); // Save #1: asigna Entry

                var noLinea = 1;
                foreach (var linea in lineasList)
                {
                    linea.Entry = creado.Entry;
                    linea.NoLinea = noLinea++;
                    await _repoGenericoDetalle.AgregarSinGuardarAsync(linea);
                }

                return creado.Entry;
            });
            // EjecutarAsync: Save #2 (líneas + consecutivo) + Commit
        }

        /// <summary>Valida la serie y asigna el número de documento (manual o el consecutivo de la serie).</summary>
        private async Task AsignarNumeroAsync(Pedido obj)
        {
            var serie = await _repoGenericoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.Bloqueado == "S")
            {
                throw new Exception("La serie está bloqueada y no se puede usar para registrar pedidos.");
            }

            if (serie.Manual == "S")
            {
                // Serie manual: el número lo escribe el usuario, el consecutivo automático no aplica.
                if (obj.NumDoc <= 0)
                {
                    throw new Exception("El número de documento es requerido para series manuales.");
                }
            }
            else
            {
                // Serie autogenerada: el consecutivo solo avanza aquí, al registrar el pedido -- no
                // al solo consultar/previsualizar el número.
                if (serie.SigNumero == null)
                {
                    throw new Exception("La serie no tiene configurado el número siguiente.");
                }

                if (serie.FinNumero.HasValue && serie.SigNumero.Value > serie.FinNumero.Value)
                {
                    throw new Exception("Se agotó la numeración disponible en esta serie.");
                }

                obj.NumDoc = serie.SigNumero.Value;

                // No se llama a _repoGenericoNumeracion.ActualizarAsync a propósito: "serie" ya es una entidad
                // rastreada por el mismo ApiDbTestContext que usan los otros repos genéricos (mismo scope de la
                // petición), así que el cambio queda pendiente y se guarda junto con el INSERT en el mismo Save --
                // si el INSERT falla, el incremento del consecutivo tampoco se guarda.
                serie.SigNumero = serie.SigNumero.Value + 1;
            }
        }

        public async Task<bool> ActualizarAsync(int id, Pedido obj)
        {
            obj.TipoObjeto = TipoObjetoPedido;

            // Un origen con documentos derivados activos no se cancela ni cambia de socio (se calcula antes de actualizar:
            // la entidad existente está rastreada y el update la pisa).
            var existente = await _repoGenericoPedido.ObtenerAsync(id);
            if (existente is not null)
            {
                var seCancela = existente.EstadoDoc != "C" && existente.Cancelado != "S" && (obj.EstadoDoc == "C" || obj.Cancelado == "S");
                var cambiaSocio = !string.Equals(existente.CodigoSn, obj.CodigoSn, StringComparison.OrdinalIgnoreCase);
                if (seCancela)
                    await _documentoBase.ValidarOrigenModificableAsync(TipoDocumentoVenta.Pedido, id, "cancelar el documento");
                else if (cambiaSocio)
                    await _documentoBase.ValidarOrigenModificableAsync(TipoDocumentoVenta.Pedido, id, "cambiar el socio de negocio");
            }

            return await _repoGenericoPedido.ActualizarAsync(id, obj);
        }

        public async Task<bool> EliminarAsync(int id)
        {
            await _documentoBase.ValidarOrigenModificableAsync(TipoDocumentoVenta.Pedido, id, "eliminar el documento");

            // No existe FK/cascada entre PedidoDetalle.Entry y Pedido.Entry en la base de datos,
            // así que las líneas de detalle se borran a mano antes que el encabezado.
            var detalles = await _repoGenericoDetalle.ObtenerTodoAsync();
            var lineas = await detalles.Where(d => d.Entry == id).ToListAsync();
            foreach (var linea in lineas)
            {
                await _repoGenericoDetalle.EliminarAsync((linea.Entry, linea.NoLinea));
            }

            return await _repoGenericoPedido.EliminarAsync(id);
        }

        public async Task<Pedido> ObtenerAsync(int id)
        {
            return await _repoGenericoPedido.ObtenerAsync(id);
        }

        public async Task<IQueryable<Pedido>> ObtenerTodoAsync()
        {
            return await _repoGenericoPedido.ObtenerTodoAsync();
        }
        #endregion
    }
}
