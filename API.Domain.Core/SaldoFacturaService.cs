using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class SaldoFacturaService : ISaldoFacturaService
    {
        // NotaCredito.BaseTipo de una nota basada en una factura de venta.
        private const int TipoBaseFactura = 6;

        private readonly IRepositorioGenerico<Factura, int> _repoFactura;
        private readonly IRepositorioGenerico<NotaCredito, int> _repoNota;
        private readonly IRepositorioGenerico<PagoRecibido, int> _repoPago;
        private readonly IRepositorioGenerico<PagoRecibidoFactura, (int Entry, int NoLinea)> _repoPagoFactura;

        public SaldoFacturaService(
            IRepositorioGenerico<Factura, int> repoFactura,
            IRepositorioGenerico<NotaCredito, int> repoNota,
            IRepositorioGenerico<PagoRecibido, int> repoPago,
            IRepositorioGenerico<PagoRecibidoFactura, (int Entry, int NoLinea)> repoPagoFactura)
        {
            _repoFactura = repoFactura;
            _repoNota = repoNota;
            _repoPago = repoPago;
            _repoPagoFactura = repoPagoFactura;
        }

        public async Task<IReadOnlyDictionary<int, SaldoFactura>> ObtenerAsync(IEnumerable<int> facturaEntries)
        {
            var resultado = new Dictionary<int, SaldoFactura>();
            var ids = (facturaEntries ?? Enumerable.Empty<int>()).Distinct().ToList();
            if (ids.Count == 0)
                return resultado;

            var facturas = await (await _repoFactura.ObtenerTodoAsync())
                .Where(f => ids.Contains(f.Entry))
                .Select(f => new { f.Entry, Total = f.TotalDoc ?? 0m })
                .ToListAsync();
            if (facturas.Count == 0)
                return resultado;

            var notas = await (await _repoNota.ObtenerTodoAsync())
                .Where(n => n.BaseTipo == TipoBaseFactura && n.Cancelado != "S" && n.BaseEntry != null && ids.Contains(n.BaseEntry.Value))
                .Select(n => new { Base = n.BaseEntry, Total = n.TotalDoc ?? 0m })
                .ToListAsync();
            var acreditado = notas
                .GroupBy(n => n.Base!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(n => n.Total));

            var lineas = await _repoPagoFactura.ObtenerTodoAsync();
            var pagos = await _repoPago.ObtenerTodoAsync();
            var aplicados = await (from pf in lineas
                                   join p in pagos on pf.Entry equals p.Entry
                                   where ids.Contains(pf.FacturaEntry) && p.Cancelado != "S"
                                   select new { pf.FacturaEntry, pf.MontoAplicado }).ToListAsync();
            var pagado = aplicados
                .GroupBy(a => a.FacturaEntry)
                .ToDictionary(g => g.Key, g => g.Sum(a => a.MontoAplicado));

            foreach (var f in facturas)
            {
                var total = Redondear(f.Total);
                var acred = Redondear(acreditado.TryGetValue(f.Entry, out var a) ? a : 0m);
                var pag = Redondear(pagado.TryGetValue(f.Entry, out var p) ? p : 0m);
                var saldo = Math.Max(0m, Redondear(total - acred - pag));
                var estado = saldo == 0m
                    ? EstadoPagoFactura.Pagada
                    : pag > 0m ? EstadoPagoFactura.Parcial : EstadoPagoFactura.Pendiente;
                resultado[f.Entry] = new SaldoFactura(f.Entry, total, acred, pag, saldo, estado);
            }
            return resultado;
        }

        public async Task<IReadOnlyList<PagoAplicado>> ObtenerPagosActivosAsync(int facturaEntry)
        {
            var lineas = await _repoPagoFactura.ObtenerTodoAsync();
            var pagos = await _repoPago.ObtenerTodoAsync();
            var filas = await (from pf in lineas
                               join p in pagos on pf.Entry equals p.Entry
                               where pf.FacturaEntry == facturaEntry && p.Cancelado != "S"
                               orderby p.Entry
                               select new { p.Entry, p.NumDoc, pf.MontoAplicado }).ToListAsync();
            return filas.Select(f => new PagoAplicado(f.Entry, f.NumDoc, f.MontoAplicado)).ToList();
        }

        private static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
    }
}
