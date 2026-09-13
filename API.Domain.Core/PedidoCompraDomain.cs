using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class PedidoCompraDomain : IPedidoCompraDomain
    {
        // Código de objeto/documento reservado para Pedidos de compra -- exigido por el CHECK
        // constraint de la tabla (TipoObjeto='11'). Se fuerza siempre en el servidor, sin confiar
        // en lo que envíe el cliente.
        private const string TipoObjetoPedidoCompra = "11";

        private readonly IRepositorioGenerico<PedidoCompra, int> _repoGenericoPedidoCompra;
        private readonly IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)> _repoGenericoDetalle;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoGenericoNumeracion;
        private readonly IRepositorioGenerico<EntregaCompra, int> _repoEntregaCompra;
        private readonly IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)> _repoEntregaCompraDetalle;
        private readonly IRepositorioGenerico<FacturaCompra, int> _repoFacturaCompra;
        private readonly IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> _repoFacturaCompraDetalle;

        public PedidoCompraDomain(
            IRepositorioGenerico<PedidoCompra, int> repoGenericoPedidoCompra,
            IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)> repoGenericoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoGenericoNumeracion,
            IRepositorioGenerico<EntregaCompra, int> repoEntregaCompra,
            IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)> repoEntregaCompraDetalle,
            IRepositorioGenerico<FacturaCompra, int> repoFacturaCompra,
            IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> repoFacturaCompraDetalle)
        {
            _repoGenericoPedidoCompra = repoGenericoPedidoCompra;
            _repoGenericoDetalle = repoGenericoDetalle;
            _repoGenericoNumeracion = repoGenericoNumeracion;
            _repoEntregaCompra = repoEntregaCompra;
            _repoEntregaCompraDetalle = repoEntregaCompraDetalle;
            _repoFacturaCompra = repoFacturaCompra;
            _repoFacturaCompraDetalle = repoFacturaCompraDetalle;
        }

        #region async methods
        public async Task<int> InsertarAsync(PedidoCompra obj)
        {
            obj.TipoObjeto = TipoObjetoPedidoCompra;

            var serie = await _repoGenericoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.Bloqueado == "S")
            {
                throw new Exception("La serie está bloqueada y no se puede usar para registrar pedidos de compra.");
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
                // Serie autogenerada: el consecutivo solo avanza aquí, al registrar el pedido de
                // compra -- no al solo consultar/previsualizar el número.
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
                // _repoGenericoPedidoCompra (ambos repos genéricos se resuelven en el mismo scope de
                // la petición), así que este cambio en memoria queda pendiente y se guarda junto con
                // el INSERT del pedido de compra en el único SaveChangesAsync de abajo -- las dos
                // operaciones quedan en la misma transacción implícita: si el INSERT falla, el
                // incremento del consecutivo tampoco se guarda.
                serie.SigNumero = serie.SigNumero.Value + 1;
            }

            var creado = await _repoGenericoPedidoCompra.InsertarAsync(obj);
            return creado.Entry;
        }

        public async Task<bool> ActualizarAsync(int id, PedidoCompra obj)
        {
            obj.TipoObjeto = TipoObjetoPedidoCompra;
            return await _repoGenericoPedidoCompra.ActualizarAsync(id, obj);
        }

        public async Task<bool> EliminarAsync(int id)
        {
            // No existe FK/cascada entre PedidoCompraDetalle.Entry y PedidoCompra.Entry en la base
            // de datos, así que las líneas de detalle se borran a mano antes que el encabezado.
            var detalles = await _repoGenericoDetalle.ObtenerTodoAsync();
            var lineas = await detalles.Where(d => d.Entry == id).ToListAsync();
            foreach (var linea in lineas)
            {
                await _repoGenericoDetalle.EliminarAsync((linea.Entry, linea.NoLinea));
            }

            return await _repoGenericoPedidoCompra.EliminarAsync(id);
        }

        public async Task<PedidoCompra> ObtenerAsync(int id)
        {
            return await _repoGenericoPedidoCompra.ObtenerAsync(id);
        }

        public async Task<IQueryable<PedidoCompra>> ObtenerTodoAsync()
        {
            return await _repoGenericoPedidoCompra.ObtenerTodoAsync();
        }

        // BaseTipo con el que una Entrega de Compra o una Factura de Compra directa apuntan a una
        // línea de Pedido de Compra -- mismo número que el TipoObjeto/CodigoObj del Pedido de Compra.
        private const int BaseTipoPedidoCompra = 11;

        public async Task<IEnumerable<DocumentoAbierto>> ObtenerAbiertosAsync(string codigoSn)
        {
            var pedidos = await (await _repoGenericoPedidoCompra.ObtenerTodoAsync())
                .Where(p => p.CodigoSn == codigoSn && p.Cancelado != "S")
                .ToListAsync();
            if (pedidos.Count == 0) return Enumerable.Empty<DocumentoAbierto>();

            var entriesPedidos = pedidos.Select(p => p.Entry).ToHashSet();
            var lineasPedido = await (await _repoGenericoDetalle.ObtenerTodoAsync())
                .Where(d => entriesPedidos.Contains(d.Entry))
                .ToListAsync();

            // Consumo por Entrega de Compra: líneas activas (encabezado no cancelado) con BaseTipo=Pedido.
            var entregasCanceladas = (await (await _repoEntregaCompra.ObtenerTodoAsync())
                .Where(e => e.Cancelado == "S")
                .Select(e => e.Entry)
                .ToListAsync()).ToHashSet();
            var consumoPorEntrega = (await (await _repoEntregaCompraDetalle.ObtenerTodoAsync())
                .Where(d => d.BaseTipo == BaseTipoPedidoCompra && d.BaseEntry != null && d.BaseLinea != null)
                .ToListAsync())
                .Where(d => !entregasCanceladas.Contains(d.Entry))
                .GroupBy(d => (d.BaseEntry!.Value, d.BaseLinea!.Value))
                .ToDictionary(g => g.Key, g => g.Sum(d => d.Cantidad ?? 0m));

            // Consumo por Factura de Compra directa (sin Entrega de por medio): mismo BaseTipo=Pedido,
            // mismo pool que el consumo por Entrega -- ver spec §1, "un solo pool compartido".
            var facturasCanceladas = (await (await _repoFacturaCompra.ObtenerTodoAsync())
                .Where(f => f.Cancelado == "S")
                .Select(f => f.Entry)
                .ToListAsync()).ToHashSet();
            var consumoPorFacturaDirecta = (await (await _repoFacturaCompraDetalle.ObtenerTodoAsync())
                .Where(d => d.BaseTipo == BaseTipoPedidoCompra && d.BaseEntry != null && d.BaseLinea != null)
                .ToListAsync())
                .Where(d => !facturasCanceladas.Contains(d.Entry))
                .GroupBy(d => (d.BaseEntry!.Value, d.BaseLinea!.Value))
                .ToDictionary(g => g.Key, g => g.Sum(d => d.Cantidad ?? 0m));

            var resultado = new List<DocumentoAbierto>();
            foreach (var pedido in pedidos)
            {
                var lineasAbiertas = new List<LineaAbierta>();
                foreach (var linea in lineasPedido.Where(l => l.Entry == pedido.Entry))
                {
                    var clave = (linea.Entry, linea.NoLinea);
                    var consumido = consumoPorEntrega.GetValueOrDefault(clave, 0m) + consumoPorFacturaDirecta.GetValueOrDefault(clave, 0m);
                    var abierta = (linea.Cantidad ?? 0m) - consumido;
                    if (abierta <= 0m) continue;

                    lineasAbiertas.Add(new LineaAbierta(
                        linea.NoLinea, linea.CodArticulo!, linea.Descripcion, linea.Cantidad ?? 0m,
                        abierta, linea.Precio, linea.CodAlmacen, linea.CodigoImpuesto));
                }
                if (lineasAbiertas.Count == 0) continue;

                resultado.Add(new DocumentoAbierto(pedido.Entry, pedido.NumDoc, pedido.Serie, pedido.FechaDoc, lineasAbiertas));
            }
            return resultado;
        }
        #endregion
    }
}
