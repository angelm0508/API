using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class FacturaCompraDomain : IFacturaCompraDomain
    {
        // Código de objeto/documento reservado para Facturas de compra -- exigido por el CHECK constraint
        // de la tabla (TipoObjeto='13'). Se fuerza siempre en el servidor, sin confiar en lo que
        // envíe el cliente.
        private const string TipoObjetoFacturaCompra = "13";

        private readonly IRepositorioGenerico<FacturaCompra, int> _repoGenericoFacturaCompra;
        private readonly IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> _repoGenericoDetalle;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoGenericoNumeracion;

        public FacturaCompraDomain(
            IRepositorioGenerico<FacturaCompra, int> repoGenericoFacturaCompra,
            IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> repoGenericoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoGenericoNumeracion)
        {
            _repoGenericoFacturaCompra = repoGenericoFacturaCompra;
            _repoGenericoDetalle = repoGenericoDetalle;
            _repoGenericoNumeracion = repoGenericoNumeracion;
        }

        #region async methods
        public async Task<int> InsertarAsync(FacturaCompra obj)
        {
            obj.TipoObjeto = TipoObjetoFacturaCompra;

            var serie = await _repoGenericoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.Bloqueado == "S")
            {
                throw new Exception("La serie está bloqueada y no se puede usar para registrar facturas de compra.");
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
                // Serie autogenerada: el consecutivo solo avanza aquí, al registrar la factura de compra -- no
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
                // _repoGenericoFacturaCompra (ambos repos genéricos se resuelven en el mismo scope de la
                // petición), así que este cambio en memoria queda pendiente y se guarda junto con
                // el INSERT de la factura de compra en el único SaveChangesAsync de abajo -- las dos operaciones
                // quedan en la misma transacción implícita: si el INSERT falla, el incremento del
                // consecutivo tampoco se guarda.
                serie.SigNumero = serie.SigNumero.Value + 1;
            }

            var creado = await _repoGenericoFacturaCompra.InsertarAsync(obj);
            return creado.Entry;
        }

        public async Task<bool> ActualizarAsync(int id, FacturaCompra obj)
        {
            obj.TipoObjeto = TipoObjetoFacturaCompra;
            return await _repoGenericoFacturaCompra.ActualizarAsync(id, obj);
        }

        public async Task<bool> EliminarAsync(int id)
        {
            // No existe FK/cascada entre FacturaCompraDetalle.Entry y FacturaCompra.Entry en la base de datos,
            // así que las líneas de detalle se borran a mano antes que el encabezado.
            var detalles = await _repoGenericoDetalle.ObtenerTodoAsync();
            var lineas = await detalles.Where(d => d.Entry == id).ToListAsync();
            foreach (var linea in lineas)
            {
                await _repoGenericoDetalle.EliminarAsync((linea.Entry, linea.NoLinea));
            }

            return await _repoGenericoFacturaCompra.EliminarAsync(id);
        }

        public async Task<FacturaCompra> ObtenerAsync(int id)
        {
            return await _repoGenericoFacturaCompra.ObtenerAsync(id);
        }

        public async Task<IQueryable<FacturaCompra>> ObtenerTodoAsync()
        {
            return await _repoGenericoFacturaCompra.ObtenerTodoAsync();
        }
        #endregion
    }
}
