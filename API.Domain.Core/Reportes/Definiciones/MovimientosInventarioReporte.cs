using API.Domain.Core.Reportes.Compartido;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core.Reportes.Definiciones
{
    /// <summary>Entradas y salidas de inventario del período con su valor firmado (la salida es negativa).</summary>
    public sealed class MovimientosInventarioReporte : IReporte
    {
        public const string CodigoReporte = "movimientos-inventario";

        private readonly IRepositorioGenerico<MovimientoInventario, int> _movimientos;

        public MovimientosInventarioReporte(IRepositorioGenerico<MovimientoInventario, int> movimientos)
        {
            _movimientos = movimientos;
        }

        public ReporteDefinicion Definicion { get; } = new(
            Codigo: CodigoReporte,
            Nombre: "Movimientos por período",
            Descripcion: "Entradas y salidas de inventario con su valor.",
            Familia: "Inventario",
            Modulo: "ReportesInventario",
            Filtros: new[]
            {
                new FiltroReporte("fechas", "Fechas", TipoFiltro.RangoFechas, Obligatorio: true),
                new FiltroReporte("articulo", "Artículo", TipoFiltro.Articulo),
                new FiltroReporte("almacen", "Almacén", TipoFiltro.Almacen),
                new FiltroReporte("tipoDoc", "Tipo de documento", TipoFiltro.Lista, Opciones: TiposDocumentoInventario.Opciones)
            },
            Columnas: new[]
            {
                new ColumnaReporte("fecha", "Fecha", TipoColumna.Fecha),
                new ColumnaReporte("tipoDoc", "Documento", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("docEntry", "Entry del documento", TipoColumna.Entero),
                new ColumnaReporte("linea", "Línea", TipoColumna.Entero),
                new ColumnaReporte("articulo", "Artículo", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("nombre", "Nombre", TipoColumna.Texto),
                new ColumnaReporte("almacen", "Almacén", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("entra", "Entra", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("sale", "Sale", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("costoUnitario", "Costo", TipoColumna.Dinero),
                new ColumnaReporte("valor", "Valor", TipoColumna.Dinero, Totaliza: Totalizacion.Suma)
            });

        public async Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite)
        {
            var desde = filtros.FechaDesde("fechas");
            var hasta = filtros.FechaHastaExclusiva("fechas");
            var articulo = filtros.Texto("articulo");
            var almacen = filtros.Texto("almacen");
            var tipoDoc = filtros.Texto("tipoDoc");

            var q = await _movimientos.ObtenerTodoAsync();
            if (desde is { } d) q = q.Where(m => m.Fecha >= d);
            if (hasta is { } h) q = q.Where(m => m.Fecha < h);
            if (articulo is not null) q = q.Where(m => m.CodArticulo == articulo);
            if (almacen is not null) q = q.Where(m => m.CodAlmacen == almacen);
            if (tipoDoc is not null) q = q.Where(m => m.TipoDoc == tipoDoc);

            var filas = await q.OrderBy(m => m.Fecha).ThenBy(m => m.Entry).Take(limite)
                .Select(m => new
                {
                    m.Fecha, m.TipoDoc, m.DocEntry, m.DocLinea, m.CodArticulo, Nombre = m.CodArticuloNavigation.Nombre,
                    m.CodAlmacen, m.CantidadEntra, m.CantidadSale, m.CostoUnitario, m.ValorMovimiento
                })
                .ToListAsync();

            return filas.Select(m => new FilaReporte
            {
                ["fecha"] = m.Fecha,
                ["tipoDoc"] = TiposDocumentoInventario.Nombre(m.TipoDoc),
                ["docEntry"] = m.DocEntry,
                ["linea"] = m.DocLinea,
                ["articulo"] = m.CodArticulo,
                ["nombre"] = m.Nombre,
                ["almacen"] = m.CodAlmacen,
                ["entra"] = m.CantidadEntra,
                ["sale"] = m.CantidadSale,
                ["costoUnitario"] = m.CostoUnitario,
                ["valor"] = m.ValorMovimiento
            }).ToList();
        }
    }
}
