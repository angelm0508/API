using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class SalidaMercanciaDomain : ISalidaMercanciaDomain
    {
        // Default de la tabla: TipoObjeto='60'. Se fuerza siempre en el servidor.
        private const string TipoObjetoSalidaMercancia = "60";

        private readonly IRepositorioGenerico<SalidaMercancia, int> _repoSalida;
        private readonly IRepositorioGenerico<SalidaMercanciaDetalle, (int Entry, int NoLinea)> _repoDetalle;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoNumeracion;
        private readonly IEjecutorTransaccion _tx;
        private readonly IInventarioAsientoService _asiento;
        private readonly IRepositorioGenerico<Articulo, string> _repoArticulo;

        public SalidaMercanciaDomain(
            IRepositorioGenerico<SalidaMercancia, int> repoSalida,
            IRepositorioGenerico<SalidaMercanciaDetalle, (int Entry, int NoLinea)> repoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoNumeracion,
            IEjecutorTransaccion tx,
            IInventarioAsientoService asiento,
            IRepositorioGenerico<Articulo, string> repoArticulo)
        {
            _repoSalida = repoSalida;
            _repoDetalle = repoDetalle;
            _repoNumeracion = repoNumeracion;
            _tx = tx;
            _asiento = asiento;
            _repoArticulo = repoArticulo;
        }

        #region async methods
        public async Task<int> InsertarAsync(SalidaMercancia obj, IEnumerable<SalidaMercanciaDetalle> lineas)
        {
            obj.TipoObjeto = TipoObjetoSalidaMercancia;
            obj.EstadoInv = "A";
            obj.Cancelado = "N";
            obj.FechaCancelado = null;

            var serie = await _repoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.Bloqueado == "S")
                throw new Exception("La serie está bloqueada y no se puede usar para registrar salidas de mercancía.");

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

            var lineasList = lineas?.ToList() ?? new List<SalidaMercanciaDetalle>();

            // Resolver el costo de cada línea y los totales ANTES de abrir la transacción:
            // el encabezado se guarda primero (Save #1) y SalidaMercancia.TotalDoc es NOT NULL,
            // así que TotalDoc tiene que estar calculado antes de ese Save.
            decimal totalDoc = 0m;
            foreach (var linea in lineasList)
            {
                var costo = await CostoVigenteAsync(linea.CodArticulo);
                linea.CostoUnitario = costo;
                linea.TotalLinea = (linea.Cantidad ?? 0m) * costo;
                totalDoc += linea.TotalLinea.Value;
            }
            obj.TotalDoc = totalDoc;

            return await _tx.EjecutarAsync(async () =>
            {
                await _repoSalida.InsertarAsync(obj); // Save #1: asigna obj.Entry

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
                        TipoDoc: TipoObjetoSalidaMercancia,
                        DocEntry: obj.Entry,
                        DocLinea: l.NoLinea,
                        CodArticulo: l.CodArticulo!,
                        CodAlmacen: l.CodAlmacen!,
                        Cantidad: -(l.Cantidad!.Value),          // negativo = salida
                        PrecioUnitario: l.CostoUnitario!.Value,  // costo ya resuelto
                        Fecha: obj.FechaContab ?? obj.FechaDoc ?? DateTime.Now))
                    .ToList();

                await _asiento.AsentarAsync(movimientos);

                return obj.Entry;
            });
            // EjecutarAsync: Save #2 (líneas + inventario) + Commit
        }

        public async Task<bool> ActualizarAsync(int id, SalidaMercancia obj)
        {
            var existente = await _repoSalida.ObtenerAsync(id);
            if (existente is null)
                return false;

            if (existente.Cancelado == "S")
                throw new Exception("El documento está cancelado y no se puede modificar.");

            // Cancelación: Cancelado pasa a 'S'.
            if (obj.Cancelado == "S")
            {
                return await _tx.EjecutarAsync(async () =>
                {
                    await _asiento.RevertirAsync(TipoObjetoSalidaMercancia, id);
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
            var existente = await _repoSalida.ObtenerAsync(id);
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

                return await _repoSalida.EliminarAsync(id);
            });
        }

        public async Task<SalidaMercancia> ObtenerAsync(int id)
        {
            return await _repoSalida.ObtenerAsync(id);
        }

        public async Task<IQueryable<SalidaMercancia>> ObtenerTodoAsync()
        {
            return await _repoSalida.ObtenerTodoAsync();
        }

        private async Task<decimal> CostoVigenteAsync(string? codArticulo)
        {
            if (codArticulo is null) return 0m;
            var art = await _repoArticulo.ObtenerAsync(codArticulo);
            if (art is null) return 0m;
            return art.MetodoValuacion == "E" ? art.CostoEstandar : art.CostoPromedio;
        }
        #endregion
    }
}
