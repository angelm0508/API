using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;

namespace API.Domain.Core
{
    public class OrdenServicioLineaService : IOrdenServicioLineaService
    {
        // CHECK constraint de la tabla: TipoObjeto='70'. Se fuerza siempre en el servidor.
        private const string TipoObjetoOrden = "70";
        private const int LongitudMaximaDescripcion = 200;

        private readonly IRepositorioGenerico<Articulo, string> _repoArticulo;
        private readonly IRepositorioGenerico<Impuesto, string> _repoImpuesto;
        private readonly IRepositorioGenerico<Mecanico, string> _repoMecanico;
        private readonly IInventarioAsientoService _asiento;

        public OrdenServicioLineaService(
            IRepositorioGenerico<Articulo, string> repoArticulo,
            IRepositorioGenerico<Impuesto, string> repoImpuesto,
            IRepositorioGenerico<Mecanico, string> repoMecanico,
            IInventarioAsientoService asiento)
        {
            _repoArticulo = repoArticulo;
            _repoImpuesto = repoImpuesto;
            _repoMecanico = repoMecanico;
            _asiento = asiento;
        }

        private static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

        public async Task PrepararAsync(IList<OrdenServicioDetalle> lineas, int numeroInicial = 1)
        {
            var tasas = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < lineas.Count; i++)
            {
                var n = numeroInicial + i;
                var linea = lineas[i];

                var codArticulo = (linea.CodArticulo ?? string.Empty).Trim();
                if (codArticulo.Length == 0)
                    throw new Exception($"Línea {n}: el artículo es requerido.");

                var articulo = await _repoArticulo.ObtenerAsync(codArticulo)
                    ?? throw new Exception($"Línea {n}: el artículo '{codArticulo}' no existe.");
                if (articulo.Activo == "N")
                    throw new Exception($"Línea {n}: el artículo '{codArticulo}' está inactivo.");
                if (articulo.ArticuloVenta != "S")
                    throw new Exception($"Línea {n}: el artículo '{codArticulo}' no está habilitado para la venta.");

                if ((linea.Cantidad ?? 0m) <= 0m)
                    throw new Exception($"Línea {n}: la cantidad debe ser mayor que 0.");

                if (linea.Precio is null)
                    throw new Exception($"Línea {n}: el precio es requerido.");
                if (linea.Precio < 0m)
                    throw new Exception($"Línea {n}: el precio no puede ser negativo.");

                var descuento = linea.PrctjeDesc ?? 0m;
                if (descuento < 0m || descuento > 100m)
                    throw new Exception($"Línea {n}: el descuento debe estar entre 0 y 100.");

                var codImpuesto = (linea.CodigoImpuesto ?? string.Empty).Trim();
                if (codImpuesto.Length == 0)
                    throw new Exception($"Línea {n}: el impuesto es requerido.");
                if (!tasas.TryGetValue(codImpuesto, out var tasa))
                {
                    var impuesto = await _repoImpuesto.ObtenerAsync(codImpuesto)
                        ?? throw new Exception($"Línea {n}: el impuesto '{codImpuesto}' no existe.");
                    tasa = impuesto.Tasa ?? 0m;
                    tasas[codImpuesto] = tasa;
                }

                var descripcion = (linea.Descripcion ?? string.Empty).Trim();
                if (descripcion.Length == 0)
                    descripcion = articulo.Nombre ?? codArticulo;
                if (descripcion.Length > LongitudMaximaDescripcion)
                    throw new Exception($"Línea {n}: la descripción no puede superar {LongitudMaximaDescripcion} caracteres.");

                var esInsumo = articulo.ArticuloInventario == "S";
                var codAlmacen = (linea.CodAlmacen ?? string.Empty).Trim();
                if (codAlmacen.Length == 0)
                    codAlmacen = (articulo.AlmacenDefecto ?? string.Empty).Trim();
                if (esInsumo && codAlmacen.Length == 0)
                    throw new Exception($"Línea {n}: el almacén es requerido para un artículo de inventario.");

                var codMecanico = (linea.CodMecanico ?? string.Empty).Trim();
                if (codMecanico.Length > 0)
                {
                    var mecanico = await _repoMecanico.ObtenerAsync(codMecanico)
                        ?? throw new Exception($"Línea {n}: el mecánico '{codMecanico}' no existe.");
                    if (mecanico.Activo == "N")
                        throw new Exception($"Línea {n}: el mecánico '{codMecanico}' está inactivo.");
                }

                // Todo validado: recién ahora se escriben los datos que fija el servidor.
                linea.CodArticulo = codArticulo;
                linea.CodigoImpuesto = codImpuesto;
                linea.Descripcion = descripcion;
                linea.CodAlmacen = codAlmacen.Length == 0 ? null : codAlmacen;
                linea.CodMecanico = codMecanico.Length == 0 ? null : codMecanico;
                linea.EsInsumo = esInsumo ? "S" : "N";
                linea.TipoObjeto = TipoObjetoOrden;
                linea.PrctjeDesc = descuento;

                var bruto = Redondear(linea.Cantidad!.Value * linea.Precio!.Value);
                var montoDesc = Redondear(bruto * descuento / 100m);
                var subtotal = bruto - montoDesc;
                var montoImpuesto = Redondear(subtotal * tasa / 100m);
                linea.Impuesto = montoImpuesto;
                linea.TotalLinea = subtotal + montoImpuesto;
            }
        }

        public async Task AsentarSalidaAsync(int entry, DateTime fecha, IEnumerable<OrdenServicioDetalle> lineas)
        {
            var movimientos = lineas
                .Where(l => l.EsInsumo == "S" && (l.Cantidad ?? 0m) > 0m)
                .Select(l => new MovimientoRequest(
                    TipoDoc: TipoObjetoOrden,
                    DocEntry: entry,
                    DocLinea: l.NoLinea,
                    CodArticulo: l.CodArticulo!,
                    CodAlmacen: l.CodAlmacen!,
                    Cantidad: -(l.Cantidad!.Value),   // negativo = salida de stock
                    PrecioUnitario: l.Precio ?? 0m,
                    Fecha: fecha))
                .ToList();

            if (movimientos.Count == 0)
                return;

            await _asiento.AsentarAsync(movimientos);   // permitirNegativo: false -> StockInsuficienteException
        }

        public void RecalcularCabecera(OrdenServicio orden, IEnumerable<OrdenServicioDetalle> lineas)
        {
            decimal totalBruto = 0m, totalDesc = 0m, totalImp = 0m, totalDoc = 0m;

            foreach (var l in lineas)
            {
                var bruto = Redondear((l.Cantidad ?? 0m) * (l.Precio ?? 0m));
                var desc = Redondear(bruto * (l.PrctjeDesc ?? 0m) / 100m);
                totalBruto += bruto;
                totalDesc += desc;
                totalImp += l.Impuesto ?? 0m;
                totalDoc += l.TotalLinea ?? 0m;
            }

            orden.TotalBruto = totalBruto;
            orden.TotalDesc = totalDesc;
            orden.TotalImp = totalImp;
            orden.TotalDoc = totalDoc;
        }
    }
}
