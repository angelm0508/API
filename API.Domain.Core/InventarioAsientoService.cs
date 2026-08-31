using API.Domain.Core.Inventario;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class InventarioAsientoService : IInventarioAsientoService
    {
        private readonly IRepositorioGenerico<Articulo, string> _repoArticulo;
        private readonly IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)> _repoExistencia;
        private readonly IRepositorioGenerico<MovimientoInventario, int> _repoMovimiento;
        private readonly IValuacionInventario _valuacion;
        private readonly IRepositorioGenerico<Almacen, string> _repoAlmacen;

        public InventarioAsientoService(
            IRepositorioGenerico<Articulo, string> repoArticulo,
            IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)> repoExistencia,
            IRepositorioGenerico<MovimientoInventario, int> repoMovimiento,
            IValuacionInventario valuacion,
            IRepositorioGenerico<Almacen, string> repoAlmacen)
        {
            _repoArticulo = repoArticulo;
            _repoExistencia = repoExistencia;
            _repoMovimiento = repoMovimiento;
            _valuacion = valuacion;
            _repoAlmacen = repoAlmacen;
        }

        public async Task AsentarAsync(IEnumerable<MovimientoRequest> movimientos, bool permitirNegativo = false)
        {
            // Procesa en el orden recibido: el promedio móvil es sensible al orden.
            foreach (var m in movimientos)
            {
                await AplicarMovimientoAsync(
                    m.TipoDoc, m.DocEntry, m.DocLinea, m.CodArticulo, m.CodAlmacen,
                    m.Cantidad, m.PrecioUnitario, m.Fecha, permitirNegativo, movReversaDe: null);
            }
        }

        public async Task RevertirAsync(string tipoDoc, int docEntry)
        {
            var queryable = await _repoMovimiento.ObtenerTodoAsync();
            var delDocumento = await queryable
                .Where(x => x.TipoDoc == tipoDoc && x.DocEntry == docEntry)
                // orden determinista: el promedio móvil es sensible al orden en que se aplican las reversas
                .OrderBy(x => x.Entry)
                .ToListAsync();

            var yaRevertidos = delDocumento
                .Where(x => x.MovReversaDe != null)
                .Select(x => x.MovReversaDe!.Value)
                .ToHashSet();

            foreach (var orig in delDocumento.Where(x => x.MovReversaDe == null && !yaRevertidos.Contains(x.Entry)))
            {
                var cantidadOriginal = orig.CantidadEntra - orig.CantidadSale;   // + entrada, - salida
                await AplicarMovimientoAsync(
                    orig.TipoDoc, orig.DocEntry, orig.DocLinea, orig.CodArticulo, orig.CodAlmacen,
                    cantidad: -cantidadOriginal,
                    // La reversa se registra como salida al costo con que se valuó el original.
                    // OJO: una salida NO recalcula el promedio móvil (ver ValuacionInventario.CalcularSalida),
                    // así que anular una compra revierte la CANTIDAD exacta pero deja CostoPromedio /
                    // ValorInventario en el valor posterior a la entrada. Ajuste de valuación en la reversa:
                    // pendiente (INV-3 / deuda de valuación conocida).
                    precioUnitario: orig.CostoUnitario,
                    fecha: orig.Fecha,
                    permitirNegativo: true,   // una reversa nunca se bloquea por negativo
                    movReversaDe: orig.Entry);
            }
        }

        private async Task AplicarMovimientoAsync(
            string tipoDoc, int docEntry, int docLinea, string codArticulo, string codAlmacen,
            decimal cantidad, decimal precioUnitario, DateTime fecha, bool permitirNegativo, int? movReversaDe)
        {
            var articulo = await _repoArticulo.ObtenerAsync(codArticulo)
                ?? throw new ArticuloNoExisteException(codArticulo);

            // Solo los artículos de inventario mueven stock; servicios/no-inventario se ignoran.
            if (articulo.ArticuloInventario != "S")
                return;

            if (await _repoAlmacen.ObtenerAsync(codAlmacen) is null)
                throw new AlmacenNoExisteException(codAlmacen);

            var existencia = await _repoExistencia.ObtenerAsync((codArticulo, codAlmacen));
            var nuevaExistencia = existencia is null;
            existencia ??= new ExistenciaArticulo { CodArticulo = codArticulo, CodAlmacen = codAlmacen, Disponible = 0m };

            var nuevaDisponible = existencia.Disponible + cantidad;
            if (nuevaDisponible < 0m && !permitirNegativo)
                throw new StockInsuficienteException(codArticulo, codAlmacen, existencia.Disponible, -cantidad);

            var cantArtActual = articulo.CantDisponible ?? 0m;
            var resultado = cantidad >= 0m
                ? _valuacion.CalcularEntrada(cantArtActual, articulo.CostoPromedio, articulo.CostoEstandar, articulo.MetodoValuacion, cantidad, precioUnitario)
                : _valuacion.CalcularSalida(cantArtActual, articulo.CostoPromedio, articulo.CostoEstandar, articulo.MetodoValuacion, -cantidad);

            // Mutaciones en memoria (entidades rastreadas por el contexto scoped).
            existencia.Disponible = nuevaDisponible;
            existencia.FechaActualizacion = DateTime.Now;
            if (nuevaExistencia)
                await _repoExistencia.AgregarSinGuardarAsync(existencia);

            articulo.CostoPromedio = resultado.NuevoCostoPromedio;
            articulo.CantDisponible = cantArtActual + cantidad;
            articulo.ValorInventario = articulo.CostoPromedio * articulo.CantDisponible.Value;

            var mov = new MovimientoInventario
            {
                TipoDoc = tipoDoc,
                DocEntry = docEntry,
                DocLinea = docLinea,
                CodArticulo = codArticulo,
                CodAlmacen = codAlmacen,
                Fecha = fecha,
                CantidadEntra = cantidad >= 0m ? cantidad : 0m,
                CantidadSale = cantidad < 0m ? -cantidad : 0m,
                PrecioUnitario = precioUnitario,
                CostoUnitario = resultado.CostoUnitarioMov,
                ValorMovimiento = resultado.ValorMovimiento,
                VariacionPrecio = resultado.VariacionPrecio,
                SaldoCantidad = articulo.CantDisponible.Value,
                SaldoCostoPromedio = articulo.CostoPromedio,
                SaldoValor = articulo.ValorInventario,
                MovReversaDe = movReversaDe
            };
            await _repoMovimiento.AgregarSinGuardarAsync(mov);
        }
    }
}
