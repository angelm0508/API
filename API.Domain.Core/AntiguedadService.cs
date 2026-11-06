using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class AntiguedadService : IAntiguedadService
    {
        private const string TipoCliente = "C";
        private const string TipoProveedor = "P";

        private readonly IRepositorioGenerico<SocioNegocio, string> _repoSocio;
        private readonly IRepositorioGenerico<Factura, int> _repoFactura;
        private readonly IRepositorioGenerico<FacturaCompra, int> _repoFacturaCompra;
        private readonly ISaldoFacturaService _saldoFactura;
        private readonly ISaldoFacturaCompraService _saldoFacturaCompra;

        public AntiguedadService(
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

        // Documento leído de la base, sin saldo todavía.
        private sealed record Documento(int Entry, string CodigoSn, int NumDoc, bool EsNotaDebito, DateTime? FechaDoc, DateTime? FechaEmision, DateTime? FechaVencimiento);

        // Documento abierto (saldo > 0) con su vencimiento y días de vencido ya calculados.
        private sealed record Abierto(Documento Doc, decimal Saldo, DateTime Vencimiento, int DiasVencido);

        public async Task<IReadOnlyList<FilaAntiguedad>> ObtenerAsync(string tipo)
        {
            ValidarTipo(tipo);

            var abiertos = await LeerAbiertosAsync(tipo, null);
            if (abiertos.Count == 0)
                return new List<FilaAntiguedad>();

            // La base compara códigos sin distinguir mayúsculas; aquí también, para no partir un socio en dos filas.
            var codigos = abiertos.Select(a => a.Doc.CodigoSn).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var socios = (await (await _repoSocio.ObtenerTodoAsync())
                    .Where(s => codigos.Contains(s.Codigo))
                    .Select(s => new { s.Codigo, s.Nombre })
                    .ToListAsync())
                .ToDictionary(s => s.Codigo, s => s, StringComparer.OrdinalIgnoreCase);

            var filas = abiertos
                .GroupBy(a => a.Doc.CodigoSn, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    // Cada saldo va a un solo tramo; ya vienen redondeados, la suma es exacta.
                    var vigente = g.Where(a => a.DiasVencido <= 0).Sum(a => a.Saldo);
                    var de1a30 = g.Where(a => a.DiasVencido >= 1 && a.DiasVencido <= 30).Sum(a => a.Saldo);
                    var de31a60 = g.Where(a => a.DiasVencido >= 31 && a.DiasVencido <= 60).Sum(a => a.Saldo);
                    var de61a90 = g.Where(a => a.DiasVencido >= 61 && a.DiasVencido <= 90).Sum(a => a.Saldo);
                    var mas90 = g.Where(a => a.DiasVencido > 90).Sum(a => a.Saldo);
                    var conocido = socios.TryGetValue(g.Key, out var socio);
                    return new FilaAntiguedad(conocido ? socio!.Codigo : g.Key, conocido ? socio!.Nombre ?? string.Empty : string.Empty,
                        vigente, de1a30, de31a60, de61a90, mas90, vigente + de1a30 + de31a60 + de61a90 + mas90);
                })
                .OrderByDescending(f => f.Total)
                .ThenBy(f => f.NombreSn, StringComparer.OrdinalIgnoreCase)
                .ThenBy(f => f.CodigoSn, StringComparer.Ordinal)
                .ToList();
            return filas;
        }

        public async Task<IReadOnlyList<DocumentoAntiguedad>> ObtenerDetalleAsync(string tipo, string codigoSn)
        {
            ValidarTipo(tipo);
            if (string.IsNullOrEmpty(codigoSn))
                return new List<DocumentoAntiguedad>();

            var abiertos = await LeerAbiertosAsync(tipo, codigoSn);
            return abiertos
                .OrderBy(a => a.Vencimiento)
                .ThenBy(a => a.Doc.NumDoc)
                .ThenBy(a => a.Doc.Entry)
                .Select(a => new DocumentoAntiguedad(a.Doc.Entry, NombreTipo(tipo, a.Doc.EsNotaDebito), a.Doc.NumDoc,
                    (a.Doc.FechaDoc ?? a.Doc.FechaEmision ?? a.Vencimiento), a.Vencimiento, a.DiasVencido, a.Saldo))
                .ToList();
        }

        private static void ValidarTipo(string tipo)
        {
            if (tipo != TipoCliente && tipo != TipoProveedor)
                throw new Exception("El tipo debe ser C o P.");
        }

        private static string NombreTipo(string tipo, bool esNotaDebito) =>
            tipo == TipoCliente
                ? (esNotaDebito ? "Nota de débito" : "Factura")
                : (esNotaDebito ? "Nota de débito de proveedor" : "Factura de compra");

        // Una consulta de documentos no cancelados del tipo (y del socio, si se pide), una llamada al servicio de saldo;
        // devuelve solo los que tienen saldo > 0.
        private async Task<List<Abierto>> LeerAbiertosAsync(string tipo, string? codigoSn)
        {
            List<Documento> docs;
            IReadOnlyDictionary<int, decimal> saldos;

            if (tipo == TipoCliente)
            {
                var consulta = (await _repoFactura.ObtenerTodoAsync())
                    .Where(f => f.CodigoSn != null
                                && (f.DocSubType == "--" || f.DocSubType == "ND")
                                && f.Cancelado != "S");
                if (codigoSn != null)
                    consulta = consulta.Where(f => f.CodigoSn == codigoSn);
                var leidos = await consulta
                    .Select(f => new { f.Entry, f.CodigoSn, f.NumDoc, f.DocSubType, f.FechaDoc, f.FechaEmision, f.FechaVencimiento })
                    .ToListAsync();
                docs = leidos
                    .Select(f => new Documento(f.Entry, f.CodigoSn!, f.NumDoc, f.DocSubType == "ND", f.FechaDoc, f.FechaEmision, f.FechaVencimiento))
                    .ToList();
                saldos = docs.Count == 0
                    ? new Dictionary<int, decimal>()
                    : (await _saldoFactura.ObtenerAsync(docs.Select(d => d.Entry).ToList())).ToDictionary(p => p.Key, p => p.Value.Saldo);
            }
            else
            {
                var consulta = (await _repoFacturaCompra.ObtenerTodoAsync())
                    .Where(f => f.CodigoSn != null
                                && (f.DocSubType == "--" || f.DocSubType == "DN")
                                && f.Cancelado != "S");
                if (codigoSn != null)
                    consulta = consulta.Where(f => f.CodigoSn == codigoSn);
                var leidos = await consulta
                    .Select(f => new { f.Entry, f.CodigoSn, f.NumDoc, f.DocSubType, f.FechaDoc, f.FechaEmision, f.FechaVencimiento })
                    .ToListAsync();
                docs = leidos
                    .Select(f => new Documento(f.Entry, f.CodigoSn!, f.NumDoc, f.DocSubType == "DN", f.FechaDoc, f.FechaEmision, f.FechaVencimiento))
                    .ToList();
                saldos = docs.Count == 0
                    ? new Dictionary<int, decimal>()
                    : (await _saldoFacturaCompra.ObtenerAsync(docs.Select(d => d.Entry).ToList())).ToDictionary(p => p.Key, p => p.Value.Saldo);
            }

            var hoy = DateTime.Today;
            var abiertos = new List<Abierto>();
            foreach (var d in docs)
            {
                var saldo = saldos.TryGetValue(d.Entry, out var s) ? Redondear(s) : 0m;
                if (saldo <= 0m)
                    continue;
                // Sin vencimiento ni fecha del documento, no hay cómo vencerlo: cuenta como vigente (igual que el saldo vencido del socio).
                var vencimiento = (d.FechaVencimiento ?? d.FechaDoc ?? d.FechaEmision ?? hoy).Date;
                abiertos.Add(new Abierto(d, saldo, vencimiento, (hoy - vencimiento).Days));
            }
            return abiertos;
        }

        private static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
    }
}
