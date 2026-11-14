using API.Domain.Core.Reportes.Compartido;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core.Reportes.Definiciones
{
    /// <summary>
    /// Movimientos de un artículo con saldo acumulado. El saldo inicial es UNA consulta agregada
    /// (Σ entra − Σ sale antes de «desde»); el acumulado se calcula en memoria sobre los movimientos del período,
    /// ordenados por fecha y entry (el recorte por límite toma los primeros, así que el acumulado es el mismo).
    /// </summary>
    public sealed class KardexArticuloReporte : IReporte
    {
        public const string CodigoReporte = "kardex-articulo";

        private readonly IRepositorioGenerico<MovimientoInventario, int> _movimientos;

        public KardexArticuloReporte(IRepositorioGenerico<MovimientoInventario, int> movimientos)
        {
            _movimientos = movimientos;
        }

        public ReporteDefinicion Definicion { get; } = new(
            Codigo: CodigoReporte,
            Nombre: "Kardex por artículo",
            Descripcion: "Movimientos de un artículo con saldo acumulado.",
            Familia: "Inventario",
            Modulo: "ReportesInventario",
            Filtros: new[]
            {
                new FiltroReporte("articulo", "Artículo", TipoFiltro.Articulo, Obligatorio: true),
                new FiltroReporte("fechas", "Fechas", TipoFiltro.RangoFechas),
                new FiltroReporte("almacen", "Almacén", TipoFiltro.Almacen)
            },
            Columnas: new[]
            {
                new ColumnaReporte("fecha", "Fecha", TipoColumna.Fecha),
                new ColumnaReporte("tipoDoc", "Documento", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("docEntry", "Entry del documento", TipoColumna.Entero),
                new ColumnaReporte("linea", "Línea", TipoColumna.Entero),
                new ColumnaReporte("almacen", "Almacén", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("entra", "Entra", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("sale", "Sale", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("costoUnitario", "Costo", TipoColumna.Dinero),
                new ColumnaReporte("valor", "Valor", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("saldo", "Saldo acumulado", TipoColumna.Cantidad),
                new ColumnaReporte("reversa", "Reversa", TipoColumna.Texto)
            });

        public async Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite)
        {
            var articulo = filtros.Texto("articulo");
            var almacen = filtros.Texto("almacen");
            var desde = filtros.FechaDesde("fechas");
            var hasta = filtros.FechaHastaExclusiva("fechas");

            var todos = (await _movimientos.ObtenerTodoAsync()).Where(m => m.CodArticulo == articulo);
            if (almacen is not null) todos = todos.Where(m => m.CodAlmacen == almacen);

            // Saldo inicial: una sola consulta agregada, sin traer los movimientos anteriores.
            var saldo = 0m;
            if (desde is { } d0)
                saldo = await todos.Where(m => m.Fecha < d0).SumAsync(m => m.CantidadEntra - m.CantidadSale);

            var periodo = todos;
            if (desde is { } d1) periodo = periodo.Where(m => m.Fecha >= d1);
            if (hasta is { } h1) periodo = periodo.Where(m => m.Fecha < h1);

            // La fila «Saldo inicial» cuenta contra el límite: nunca se devuelven más de `limite` filas.
            var topeMovimientos = Math.Max(saldo != 0m ? limite - 1 : limite, 0);
            var movimientos = await periodo.OrderBy(m => m.Fecha).ThenBy(m => m.Entry).Take(topeMovimientos)
                .Select(m => new
                {
                    m.Fecha, m.TipoDoc, m.DocEntry, m.DocLinea, m.CodAlmacen, m.CantidadEntra, m.CantidadSale,
                    m.CostoUnitario, m.ValorMovimiento, EsReversa = m.MovReversaDe != null
                })
                .ToListAsync();

            var filas = new List<FilaReporte>(movimientos.Count + 1);

            if (saldo != 0m)
            {
                filas.Add(new FilaReporte
                {
                    ["fecha"] = desde,
                    ["tipoDoc"] = "Saldo inicial",
                    ["docEntry"] = null,
                    ["linea"] = null,
                    ["almacen"] = almacen,
                    ["entra"] = 0m,
                    ["sale"] = 0m,
                    ["costoUnitario"] = null,
                    ["valor"] = 0m,
                    ["saldo"] = saldo,
                    ["reversa"] = ""
                });
            }

            foreach (var m in movimientos)
            {
                saldo += m.CantidadEntra - m.CantidadSale;
                filas.Add(new FilaReporte
                {
                    ["fecha"] = m.Fecha,
                    ["tipoDoc"] = TiposDocumentoInventario.Nombre(m.TipoDoc),
                    ["docEntry"] = m.DocEntry,
                    ["linea"] = m.DocLinea,
                    ["almacen"] = m.CodAlmacen,
                    ["entra"] = m.CantidadEntra,
                    ["sale"] = m.CantidadSale,
                    ["costoUnitario"] = m.CostoUnitario,
                    ["valor"] = m.ValorMovimiento,
                    ["saldo"] = saldo,
                    ["reversa"] = m.EsReversa ? "Sí" : ""
                });
            }

            return filas;
        }
    }
}
