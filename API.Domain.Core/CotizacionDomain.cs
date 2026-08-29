using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class CotizacionDomain : ICotizacionDomain
    {
        // Código de objeto/documento reservado para Cotizaciones -- exigido por el CHECK
        // constraint de la tabla (CK_Cotizacion_TipoObjeto). Se fuerza siempre en el servidor,
        // sin confiar en lo que envíe el cliente.
        private const string TipoObjetoCotizacion = "3";

        private readonly IRepositorioGenerico<Cotizacion, int> _repoGenericoCotizacion;
        private readonly IRepositorioGenerico<CotizacionDetalle, (int Entry, int NoLinea)> _repoGenericoDetalle;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoGenericoNumeracion;

        public CotizacionDomain(
            IRepositorioGenerico<Cotizacion, int> repoGenericoCotizacion,
            IRepositorioGenerico<CotizacionDetalle, (int Entry, int NoLinea)> repoGenericoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoGenericoNumeracion)
        {
            _repoGenericoCotizacion = repoGenericoCotizacion;
            _repoGenericoDetalle = repoGenericoDetalle;
            _repoGenericoNumeracion = repoGenericoNumeracion;
        }

        #region async methods
        public async Task<int> InsertarAsync(Cotizacion obj)
        {
            obj.TipoObjeto = TipoObjetoCotizacion;

            var serie = await _repoGenericoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.Bloqueado == "S")
            {
                throw new Exception("La serie está bloqueada y no se puede usar para registrar cotizaciones.");
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
                // Serie autogenerada: el consecutivo solo avanza aquí, al registrar la cotización
                // -- no al solo consultar/previsualizar el número (a diferencia del endpoint
                // compartido NumeracionDocumentoDet.GenerarCodigoAsync, que sí lo hacía de
                // inmediato y "quemaba" números aunque el documento nunca se guardara).
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
                // _repoGenericoCotizacion (ambos repos genéricos se resuelven en el mismo scope de
                // la petición), así que este cambio en memoria queda pendiente y se guarda junto
                // con el INSERT de la cotización en el único SaveChangesAsync de abajo -- las dos
                // operaciones quedan en la misma transacción implícita: si el INSERT falla, el
                // incremento del consecutivo tampoco se guarda.
                serie.SigNumero = serie.SigNumero.Value + 1;
            }

            var creado = await _repoGenericoCotizacion.InsertarAsync(obj);
            return creado.Entry;
        }

        public async Task<bool> ActualizarAsync(int id, Cotizacion obj)
        {
            obj.TipoObjeto = TipoObjetoCotizacion;
            return await _repoGenericoCotizacion.ActualizarAsync(id, obj);
        }

        public async Task<bool> EliminarAsync(int id)
        {
            // No existe FK/cascada entre CotizacionDetalle.Entry y Cotizacion.Entry en la base de
            // datos, así que las líneas de detalle se borran a mano antes que el encabezado.
            var detalles = await _repoGenericoDetalle.ObtenerTodoAsync();
            var lineas = await detalles.Where(d => d.Entry == id).ToListAsync();
            foreach (var linea in lineas)
            {
                await _repoGenericoDetalle.EliminarAsync((linea.Entry, linea.NoLinea));
            }

            return await _repoGenericoCotizacion.EliminarAsync(id);
        }

        public async Task<Cotizacion> ObtenerAsync(int id)
        {
            return await _repoGenericoCotizacion.ObtenerAsync(id);
        }

        public async Task<IQueryable<Cotizacion>> ObtenerTodoAsync()
        {
            return await _repoGenericoCotizacion.ObtenerTodoAsync();
        }
        #endregion
    }
}
