using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class EntregaDomain : IEntregaDomain
    {
        // Código de objeto/documento reservado para Entregas -- exigido por el CHECK constraint
        // de la tabla (TipoObjeto='4'). Se fuerza siempre en el servidor, sin confiar en lo que
        // envíe el cliente.
        private const string TipoObjetoEntrega = "5";

        private readonly IRepositorioGenerico<Entrega, int> _repoGenericoEntrega;
        private readonly IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)> _repoGenericoDetalle;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoGenericoNumeracion;

        public EntregaDomain(
            IRepositorioGenerico<Entrega, int> repoGenericoEntrega,
            IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)> repoGenericoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoGenericoNumeracion)
        {
            _repoGenericoEntrega = repoGenericoEntrega;
            _repoGenericoDetalle = repoGenericoDetalle;
            _repoGenericoNumeracion = repoGenericoNumeracion;
        }

        #region async methods
        public async Task<int> InsertarAsync(Entrega obj)
        {
            obj.TipoObjeto = TipoObjetoEntrega;

            var serie = await _repoGenericoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.Bloqueado == "S")
            {
                throw new Exception("La serie está bloqueada y no se puede usar para registrar entregas.");
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
                // Serie autogenerada: el consecutivo solo avanza aquí, al registrar el entrega -- no
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
                // _repoGenericoEntrega (ambos repos genéricos se resuelven en el mismo scope de la
                // petición), así que este cambio en memoria queda pendiente y se guarda junto con
                // el INSERT del entrega en el único SaveChangesAsync de abajo -- las dos operaciones
                // quedan en la misma transacción implícita: si el INSERT falla, el incremento del
                // consecutivo tampoco se guarda.
                serie.SigNumero = serie.SigNumero.Value + 1;
            }

            var creado = await _repoGenericoEntrega.InsertarAsync(obj);
            return creado.Entry;
        }

        public async Task<bool> ActualizarAsync(int id, Entrega obj)
        {
            obj.TipoObjeto = TipoObjetoEntrega;
            return await _repoGenericoEntrega.ActualizarAsync(id, obj);
        }

        public async Task<bool> EliminarAsync(int id)
        {
            // No existe FK/cascada entre EntregaDetalle.Entry y Entrega.Entry en la base de datos,
            // así que las líneas de detalle se borran a mano antes que el encabezado.
            var detalles = await _repoGenericoDetalle.ObtenerTodoAsync();
            var lineas = await detalles.Where(d => d.Entry == id).ToListAsync();
            foreach (var linea in lineas)
            {
                await _repoGenericoDetalle.EliminarAsync((linea.Entry, linea.NoLinea));
            }

            return await _repoGenericoEntrega.EliminarAsync(id);
        }

        public async Task<Entrega> ObtenerAsync(int id)
        {
            return await _repoGenericoEntrega.ObtenerAsync(id);
        }

        public async Task<IQueryable<Entrega>> ObtenerTodoAsync()
        {
            return await _repoGenericoEntrega.ObtenerTodoAsync();
        }
        #endregion
    }
}
