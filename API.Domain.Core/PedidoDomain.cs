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

        public PedidoDomain(
            IRepositorioGenerico<Pedido, int> repoGenericoPedido,
            IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)> repoGenericoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoGenericoNumeracion)
        {
            _repoGenericoPedido = repoGenericoPedido;
            _repoGenericoDetalle = repoGenericoDetalle;
            _repoGenericoNumeracion = repoGenericoNumeracion;
        }

        #region async methods
        public async Task<int> InsertarAsync(Pedido obj)
        {
            obj.TipoObjeto = TipoObjetoPedido;

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

                // No se llama a _repoGenericoNumeracion.ActualizarAsync aquí a propósito: "serie"
                // ya es una entidad rastreada por el mismo ApiDbTestContext que usa
                // _repoGenericoPedido (ambos repos genéricos se resuelven en el mismo scope de la
                // petición), así que este cambio en memoria queda pendiente y se guarda junto con
                // el INSERT del pedido en el único SaveChangesAsync de abajo -- las dos operaciones
                // quedan en la misma transacción implícita: si el INSERT falla, el incremento del
                // consecutivo tampoco se guarda.
                serie.SigNumero = serie.SigNumero.Value + 1;
            }

            var creado = await _repoGenericoPedido.InsertarAsync(obj);
            return creado.Entry;
        }

        public async Task<bool> ActualizarAsync(int id, Pedido obj)
        {
            obj.TipoObjeto = TipoObjetoPedido;
            return await _repoGenericoPedido.ActualizarAsync(id, obj);
        }

        public async Task<bool> EliminarAsync(int id)
        {
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
