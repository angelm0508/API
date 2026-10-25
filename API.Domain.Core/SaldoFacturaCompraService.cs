using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class SaldoFacturaCompraService : ISaldoFacturaCompraService
    {
        // NotaCreditoCompra.BaseTipo de una nota basada en una factura de compra.
        private const int TipoBaseFactura = 13;

        private readonly IRepositorioGenerico<FacturaCompra, int> _repoFactura;
        private readonly IRepositorioGenerico<NotaCreditoCompra, int> _repoNota;
        private readonly IRepositorioGenerico<PagoEfectuado, int> _repoPago;
        private readonly IRepositorioGenerico<PagoEfectuadoFactura, (int Entry, int NoLinea)> _repoPagoFactura;

        public SaldoFacturaCompraService(
            IRepositorioGenerico<FacturaCompra, int> repoFactura,
            IRepositorioGenerico<NotaCreditoCompra, int> repoNota,
            IRepositorioGenerico<PagoEfectuado, int> repoPago,
            IRepositorioGenerico<PagoEfectuadoFactura, (int Entry, int NoLinea)> repoPagoFactura)
        {
            _repoFactura = repoFactura;
            _repoNota = repoNota;
            _repoPago = repoPago;
            _repoPagoFactura = repoPagoFactura;
        }

        public async Task<IReadOnlyDictionary<int, SaldoFacturaCompra>> ObtenerAsync(IEnumerable<int> facturaCompraEntries)
        {
            var resultado = new Dictionary<int, SaldoFacturaCompra>();
            var ids = (facturaCompraEntries ?? Enumerable.Empty<int>()).Distinct().ToList();
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
                                   where ids.Contains(pf.FacturaCompraEntry) && p.Cancelado != "S"
                                   select new { pf.FacturaCompraEntry, pf.MontoAplicado }).ToListAsync();
            var pagado = aplicados
                .GroupBy(a => a.FacturaCompraEntry)
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
                resultado[f.Entry] = new SaldoFacturaCompra(f.Entry, total, acred, pag, saldo, estado);
            }
            return resultado;
        }

        public async Task<IReadOnlyList<PagoAplicado>> ObtenerPagosActivosAsync(int facturaCompraEntry)
        {
            var lineas = await _repoPagoFactura.ObtenerTodoAsync();
            var pagos = await _repoPago.ObtenerTodoAsync();
            var filas = await (from pf in lineas
                               join p in pagos on pf.Entry equals p.Entry
                               where pf.FacturaCompraEntry == facturaCompraEntry && p.Cancelado != "S"
                               orderby p.Entry
                               select new { p.Entry, p.NumDoc, pf.MontoAplicado }).ToListAsync();
            return filas.Select(f => new PagoAplicado(f.Entry, f.NumDoc, f.MontoAplicado)).ToList();
        }

        public async Task<bool> TienePagosRegistradosAsync(int facturaCompraEntry)
        {
            var lineas = await _repoPagoFactura.ObtenerTodoAsync();
            return await lineas.AnyAsync(pf => pf.FacturaCompraEntry == facturaCompraEntry);
        }

        private static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
    }
}
