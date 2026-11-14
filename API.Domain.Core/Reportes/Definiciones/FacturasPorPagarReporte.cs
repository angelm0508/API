using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core.Reportes.Definiciones
{
    /// <summary>
    /// Facturas y notas de débito de proveedores con su saldo, vencimiento y días de atraso.
    /// El saldo y el estado de pago son los ACTUALES (los calcula <see cref="ISaldoFacturaCompraService"/> al día de hoy);
    /// la fecha de corte solo define qué documentos se incluyen (FechaDoc ≤ corte, día completo) y los días de atraso.
    /// El recorte por <c>limite</c> se aplica DESPUÉS de filtrar por saldo/estado/vencidas, por lo que se leen los
    /// documentos candidatos (solo las columnas usadas) y los saldos se piden en lotes de 1,000.
    /// </summary>
    public sealed class FacturasPorPagarReporte : IReporte
    {
        public const string CodigoReporte = "facturas-por-pagar";

        private const int TamanoLote = 1000;

        private readonly IRepositorioGenerico<FacturaCompra, int> _facturas;
        private readonly ISaldoFacturaCompraService _saldos;

        public FacturasPorPagarReporte(IRepositorioGenerico<FacturaCompra, int> facturas, ISaldoFacturaCompraService saldos)
        {
            _facturas = facturas;
            _saldos = saldos;
        }

        public ReporteDefinicion Definicion { get; } = new(
            Codigo: CodigoReporte,
            Nombre: "Facturas por pagar",
            Descripcion: "Facturas y notas de débito de proveedores con su saldo, vencimiento y días de atraso.",
            Familia: "Cobros y pagos",
            Modulo: "ReportesCobrosPagos",
            Filtros: new[]
            {
                new FiltroReporte("proveedor", "Proveedor", TipoFiltro.Socio, SubTipo: "P"),
                new FiltroReporte("estadoPago", "Estado de pago", TipoFiltro.Lista, Opciones: new[]
                {
                    new OpcionFiltro("ABIERTAS", "Pendientes y parciales"),
                    new OpcionFiltro("PENDIENTE", "Pendientes"),
                    new OpcionFiltro("PARCIAL", "Parciales"),
                    new OpcionFiltro("PAGADA", "Pagadas"),
                    new OpcionFiltro("TODAS", "Todas")
                }, ValorPorDefecto: "ABIERTAS"),
                new FiltroReporte("soloVencidas", "Solo vencidas", TipoFiltro.Lista, Opciones: new[]
                {
                    new OpcionFiltro("N", "No"),
                    new OpcionFiltro("S", "Sí")
                }, ValorPorDefecto: "N"),
                new FiltroReporte("fechaCorte", "Fecha de corte", TipoFiltro.Fecha)
            },
            Columnas: new[]
            {
                new ColumnaReporte("fecha", "Fecha", TipoColumna.Fecha),
                new ColumnaReporte("tipo", "Tipo", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("serie", "Serie", TipoColumna.Entero, VisiblePorDefecto: false),
                new ColumnaReporte("numDoc", "No.", TipoColumna.Entero),
                new ColumnaReporte("codigoSn", "Código proveedor", TipoColumna.Texto, VisiblePorDefecto: false),
                new ColumnaReporte("nombreSn", "Proveedor", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("vencimiento", "Vencimiento", TipoColumna.Fecha),
                new ColumnaReporte("moneda", "Moneda", TipoColumna.Texto, Agrupable: true, VisiblePorDefecto: false),
                new ColumnaReporte("total", "Total", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("acreditado", "Notas de crédito", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("pagado", "Pagado", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("saldo", "Saldo", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("estado", "Estado", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("diasAtraso", "Días de atraso", TipoColumna.Entero)
            });

        public async Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite)
        {
            var corte = (filtros.Fecha("fechaCorte") ?? DateTime.Today).Date;
            var corteExclusivo = corte.AddDays(1);
            var proveedor = filtros.Texto("proveedor");
            var estadoPago = filtros.Texto("estadoPago") ?? "ABIERTAS";
            var soloVencidas = filtros.Texto("soloVencidas") == "S";

            var q = (await _facturas.ObtenerTodoAsync())
                .Where(f => f.Cancelado != "S" && (f.DocSubType == "--" || f.DocSubType == "DN") && f.FechaDoc < corteExclusivo);
            if (proveedor is not null) q = q.Where(f => f.CodigoSn == proveedor);

            var candidatas = await q
                .Select(f => new Candidata
                {
                    Entry = f.Entry,
                    Fecha = f.FechaDoc,
                    Vencimiento = f.FechaVencimiento ?? f.FechaDoc,
                    Serie = f.Serie,
                    NumDoc = f.NumDoc,
                    EsNotaDebito = f.DocSubType == "DN",
                    CodigoSn = f.CodigoSn,
                    NombreSn = f.NombreSn,
                    Moneda = f.MonedaDoc
                })
                .ToListAsync();

            var filas = new List<(Candidata Doc, SaldoFacturaCompra Saldo, int DiasAtraso)>();
            for (var i = 0; i < candidatas.Count; i += TamanoLote)
            {
                var lote = candidatas.Skip(i).Take(TamanoLote).ToList();
                var saldos = await _saldos.ObtenerAsync(lote.Select(c => c.Entry).ToList());
                foreach (var doc in lote)
                {
                    if (!saldos.TryGetValue(doc.Entry, out var saldo)) continue;   // factura ausente del servicio: se omite
                    if (!CoincideEstado(estadoPago, saldo.Estado)) continue;

                    var vencida = saldo.Saldo > 0m && doc.Vencimiento is { } v && v.Date < corte;
                    if (soloVencidas && !vencida) continue;

                    filas.Add((doc, saldo, vencida ? (corte - doc.Vencimiento!.Value.Date).Days : 0));
                }
            }

            return filas
                .OrderBy(x => x.Doc.Vencimiento?.Date)
                .ThenBy(x => x.Doc.Fecha?.Date)
                .ThenBy(x => x.Doc.NumDoc)
                .ThenBy(x => x.Doc.Entry)
                .Take(limite)
                .Select(x => new FilaReporte
                {
                    ["fecha"] = x.Doc.Fecha,
                    ["tipo"] = x.Doc.EsNotaDebito ? "Nota de débito de compra" : "Factura de compra",
                    ["serie"] = x.Doc.Serie,
                    ["numDoc"] = x.Doc.NumDoc,
                    ["codigoSn"] = x.Doc.CodigoSn,
                    ["nombreSn"] = x.Doc.NombreSn,
                    ["vencimiento"] = x.Doc.Vencimiento,
                    ["moneda"] = x.Doc.Moneda,
                    ["total"] = x.Saldo.Total,
                    ["acreditado"] = x.Saldo.Acreditado,
                    ["pagado"] = x.Saldo.Pagado,
                    ["saldo"] = x.Saldo.Saldo,
                    ["estado"] = x.Saldo.Estado,
                    ["diasAtraso"] = x.DiasAtraso
                })
                .ToList();
        }

        private static bool CoincideEstado(string filtro, string estado) => filtro switch
        {
            "PENDIENTE" => estado == EstadoPagoFactura.Pendiente,
            "PARCIAL" => estado == EstadoPagoFactura.Parcial,
            "PAGADA" => estado == EstadoPagoFactura.Pagada,
            "TODAS" => true,
            _ => estado == EstadoPagoFactura.Pendiente || estado == EstadoPagoFactura.Parcial   // ABIERTAS
        };

        private sealed class Candidata
        {
            public int Entry { get; set; }
            public DateTime? Fecha { get; set; }
            public DateTime? Vencimiento { get; set; }
            public int Serie { get; set; }
            public int NumDoc { get; set; }
            public bool EsNotaDebito { get; set; }
            public string? CodigoSn { get; set; }
            public string? NombreSn { get; set; }
            public string? Moneda { get; set; }
        }
    }
}
