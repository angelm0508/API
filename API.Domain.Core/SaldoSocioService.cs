using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class SaldoSocioService : ISaldoSocioService
    {
        private const string TipoCliente = "C";
        private const string TipoProveedor = "P";

        private readonly IRepositorioGenerico<SocioNegocio, string> _repoSocio;
        private readonly IRepositorioGenerico<Factura, int> _repoFactura;
        private readonly IRepositorioGenerico<FacturaCompra, int> _repoFacturaCompra;
        private readonly ISaldoFacturaService _saldoFactura;
        private readonly ISaldoFacturaCompraService _saldoFacturaCompra;

        public SaldoSocioService(
            IRepositorioGenerico<SocioNegocio, string> repoSocio,
            IRepositorioGenerico<Factura, int> repoFactura,
            IRepositorioGenerico<FacturaCompra, int> repoFacturaCompra,
            ISaldoFacturaService saldoFactura,
            ISaldoFacturaCompraService saldoFacturaCompra)
        {
            _repoSocio = repoSocio;
            _repoFactura = repoFactura;
            _repoFacturaCompra = repoFacturaCompra;
            _saldoFactura = saldoFactura;
            _saldoFacturaCompra = saldoFacturaCompra;
        }

        public async Task<IReadOnlyDictionary<string, SaldoSocio>> ObtenerAsync(IEnumerable<string> codigosSn)
        {
            var resultado = new Dictionary<string, SaldoSocio>();
            var codigos = (codigosSn ?? Enumerable.Empty<string>())
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .ToList();
            if (codigos.Count == 0)
                return resultado;

            var socios = await (await _repoSocio.ObtenerTodoAsync())
                .Where(s => codigos.Contains(s.Codigo))
                .Select(s => new { s.Codigo, s.TipoSn })
                .ToListAsync();
            if (socios.Count == 0)
                return resultado;

            var hoy = DateTime.Today;
            var clientes = socios.Where(s => s.TipoSn == TipoCliente).Select(s => s.Codigo).ToList();
            var proveedores = socios.Where(s => s.TipoSn == TipoProveedor).Select(s => s.Codigo).ToList();

            // Por socio: (documentos abiertos, saldo, vencido).
            var acumulado = new Dictionary<string, (int Abiertos, decimal Saldo, decimal Vencido)>();

            if (clientes.Count > 0)
            {
                var docs = await (await _repoFactura.ObtenerTodoAsync())
                    .Where(f => f.CodigoSn != null && clientes.Contains(f.CodigoSn)
                                && (f.DocSubType == "--" || f.DocSubType == "ND")
                                && f.Cancelado != "S")
                    .Select(f => new { f.Entry, f.CodigoSn, f.FechaDoc, f.FechaVencimiento })
                    .ToListAsync();
                if (docs.Count > 0)
                {
                    var saldos = await _saldoFactura.ObtenerAsync(docs.Select(d => d.Entry).ToList());
                    foreach (var d in docs)
                        Acumular(acumulado, d.CodigoSn!, saldos.TryGetValue(d.Entry, out var s) ? s.Saldo : 0m, d.FechaVencimiento ?? d.FechaDoc, hoy);
                }
            }

            if (proveedores.Count > 0)
            {
                var docs = await (await _repoFacturaCompra.ObtenerTodoAsync())
                    .Where(f => f.CodigoSn != null && proveedores.Contains(f.CodigoSn)
                                && (f.DocSubType == "--" || f.DocSubType == "DN")
                                && f.Cancelado != "S")
                    .Select(f => new { f.Entry, f.CodigoSn, f.FechaDoc, f.FechaVencimiento })
                    .ToListAsync();
                if (docs.Count > 0)
                {
                    var saldos = await _saldoFacturaCompra.ObtenerAsync(docs.Select(d => d.Entry).ToList());
                    foreach (var d in docs)
                        Acumular(acumulado, d.CodigoSn!, saldos.TryGetValue(d.Entry, out var s) ? s.Saldo : 0m, d.FechaVencimiento ?? d.FechaDoc, hoy);
                }
            }

            foreach (var socio in socios)
            {
                acumulado.TryGetValue(socio.Codigo, out var a);
                resultado[socio.Codigo] = new SaldoSocio(socio.Codigo, socio.TipoSn ?? string.Empty, Redondear(a.Saldo), Redondear(a.Vencido), a.Abiertos);
            }
            return resultado;
        }

        // Suma el documento al socio solo si tiene saldo; es vencido si su vencimiento (o FechaDoc) es anterior a hoy.
        private static void Acumular(Dictionary<string, (int Abiertos, decimal Saldo, decimal Vencido)> acumulado,
            string codigoSn, decimal saldo, DateTime? vencimiento, DateTime hoy)
        {
            if (saldo <= 0m)
                return;
            acumulado.TryGetValue(codigoSn, out var a);
            var vencido = vencimiento.HasValue && vencimiento.Value.Date < hoy ? saldo : 0m;
            acumulado[codigoSn] = (a.Abiertos + 1, a.Saldo + saldo, a.Vencido + vencido);
        }

        private static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
    }
}
