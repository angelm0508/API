using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class EstadoCuentaService : IEstadoCuentaService
    {
        private const string TipoProveedor = "P";
        // BaseTipo de una nota de crédito basada en una factura de venta / de compra.
        private const int TipoBaseFacturaVenta = 6;
        private const int TipoBaseFacturaCompra = 13;
        private const int LongitudMaximaDescripcion = 100;

        private readonly IRepositorioGenerico<SocioNegocio, string> _repoSocio;
        private readonly IRepositorioGenerico<Factura, int> _repoFactura;
        private readonly IRepositorioGenerico<NotaCredito, int> _repoNota;
        private readonly IRepositorioGenerico<PagoRecibido, int> _repoPago;
        private readonly IRepositorioGenerico<FacturaCompra, int> _repoFacturaCompra;
        private readonly IRepositorioGenerico<NotaCreditoCompra, int> _repoNotaCompra;
        private readonly IRepositorioGenerico<PagoEfectuado, int> _repoPagoEfectuado;
        private readonly ISaldoSocioService _saldoSocio;

        public EstadoCuentaService(
            IRepositorioGenerico<SocioNegocio, string> repoSocio,
            IRepositorioGenerico<Factura, int> repoFactura,
            IRepositorioGenerico<NotaCredito, int> repoNota,
            IRepositorioGenerico<PagoRecibido, int> repoPago,
            IRepositorioGenerico<FacturaCompra, int> repoFacturaCompra,
            IRepositorioGenerico<NotaCreditoCompra, int> repoNotaCompra,
            IRepositorioGenerico<PagoEfectuado, int> repoPagoEfectuado,
            ISaldoSocioService saldoSocio)
        {
            _repoSocio = repoSocio;
            _repoFactura = repoFactura;
            _repoNota = repoNota;
            _repoPago = repoPago;
            _repoFacturaCompra = repoFacturaCompra;
            _repoNotaCompra = repoNotaCompra;
            _repoPagoEfectuado = repoPagoEfectuado;
            _saldoSocio = saldoSocio;
        }

        // Documento ya normalizado, antes de acumular: fecha sin hora, cargo/abono redondeados.
        private sealed record Renglon(DateTime Fecha, string Tipo, int NumDoc, string Descripcion, decimal Cargo, decimal Abono, bool Informativo);

        public async Task<EstadoCuenta> ObtenerAsync(string codigoSn, DateTime? desde, DateTime? hasta)
        {
            var hastaFecha = (hasta ?? DateTime.Today).Date;
            var desdeFecha = (desde ?? new DateTime(DateTime.Today.Year, 1, 1)).Date;
            if (desdeFecha > hastaFecha)
                throw new Exception("La fecha inicial no puede ser posterior a la final.");

            var socio = await (await _repoSocio.ObtenerTodoAsync())
                .Where(s => s.Codigo == codigoSn)
                .FirstOrDefaultAsync();
            if (socio == null)
                throw new Exception("El socio de negocio no existe.");

            var renglones = socio.TipoSn == TipoProveedor
                ? await LeerProveedorAsync(socio.Codigo)
                : await LeerClienteAsync(socio.Codigo);

            var saldoInicial = Redondear(renglones.Where(r => r.Fecha < desdeFecha).Sum(r => r.Cargo - r.Abono));

            var acumulado = saldoInicial;
            var movimientos = new List<MovimientoCuenta>();
            foreach (var r in renglones
                .Where(r => r.Fecha >= desdeFecha && r.Fecha <= hastaFecha)
                .OrderBy(r => r.Fecha)
                .ThenBy(r => r.Tipo, StringComparer.Ordinal)
                .ThenBy(r => r.NumDoc))
            {
                acumulado = Redondear(acumulado + r.Cargo - r.Abono);
                movimientos.Add(new MovimientoCuenta(r.Fecha, r.Tipo, r.NumDoc, r.Descripcion, r.Cargo, r.Abono, acumulado, r.Informativo));
            }

            var saldos = await _saldoSocio.ObtenerAsync(new[] { socio.Codigo });
            saldos.TryGetValue(socio.Codigo, out var saldoActual);

            return new EstadoCuenta(socio, desdeFecha, hastaFecha, saldoInicial, movimientos, acumulado,
                saldoActual?.Saldo ?? 0m, saldoActual?.Vencido ?? 0m);
        }

        private async Task<List<Renglon>> LeerClienteAsync(string codigo)
        {
            var facturas = await (await _repoFactura.ObtenerTodoAsync())
                .Where(f => f.CodigoSn == codigo && (f.DocSubType == "--" || f.DocSubType == "ND") && f.Cancelado != "S")
                .Select(f => new { f.NumDoc, f.FechaDoc, f.FechaEmision, f.DocSubType, f.TotalDoc, f.Comentario })
                .ToListAsync();
            var notas = await (await _repoNota.ObtenerTodoAsync())
                .Where(n => n.CodigoSn == codigo && n.Cancelado != "S")
                .Select(n => new { n.NumDoc, n.FechaDoc, n.FechaEmision, n.BaseTipo, n.BaseEntry, n.TotalDoc, n.Comentario })
                .ToListAsync();
            var pagos = await (await _repoPago.ObtenerTodoAsync())
                .Where(p => p.CodigoSn == codigo && p.Cancelado != "S")
                .Select(p => new { p.NumDoc, p.FechaDoc, p.TotalPago, p.Comentario })
                .ToListAsync();

            var renglones = new List<Renglon>();
            foreach (var f in facturas)
            {
                var tipo = f.DocSubType == "ND" ? "Nota de débito" : "Factura";
                renglones.Add(Cargo(Fecha(f.FechaDoc, f.FechaEmision), tipo, f.NumDoc, f.Comentario, f.TotalDoc ?? 0m));
            }
            foreach (var n in notas)
                renglones.Add(NotaCredito("Nota de crédito", Fecha(n.FechaDoc, n.FechaEmision), n.NumDoc, n.Comentario,
                    n.TotalDoc ?? 0m, n.BaseTipo == TipoBaseFacturaVenta && n.BaseEntry != null));
            foreach (var p in pagos)
                renglones.Add(Abono(Fecha(p.FechaDoc, null), "Pago", p.NumDoc, p.Comentario, p.TotalPago));
            return renglones;
        }

        private async Task<List<Renglon>> LeerProveedorAsync(string codigo)
        {
            var facturas = await (await _repoFacturaCompra.ObtenerTodoAsync())
                .Where(f => f.CodigoSn == codigo && (f.DocSubType == "--" || f.DocSubType == "DN") && f.Cancelado != "S")
                .Select(f => new { f.NumDoc, f.FechaDoc, f.FechaEmision, f.DocSubType, f.TotalDoc, f.Comentario })
                .ToListAsync();
            var notas = await (await _repoNotaCompra.ObtenerTodoAsync())
                .Where(n => n.CodigoSn == codigo && n.Cancelado != "S")
                .Select(n => new { n.NumDoc, n.FechaDoc, n.FechaEmision, n.BaseTipo, n.BaseEntry, n.TotalDoc, n.Comentario })
                .ToListAsync();
            var pagos = await (await _repoPagoEfectuado.ObtenerTodoAsync())
                .Where(p => p.CodigoSn == codigo && p.Cancelado != "S")
                .Select(p => new { p.NumDoc, p.FechaDoc, p.TotalPago, p.Comentario })
                .ToListAsync();

            var renglones = new List<Renglon>();
            foreach (var f in facturas)
            {
                var tipo = f.DocSubType == "DN" ? "Nota de débito de proveedor" : "Factura de compra";
                renglones.Add(Cargo(Fecha(f.FechaDoc, f.FechaEmision), tipo, f.NumDoc, f.Comentario, f.TotalDoc ?? 0m));
            }
            foreach (var n in notas)
                renglones.Add(NotaCredito("Nota de crédito de proveedor", Fecha(n.FechaDoc, n.FechaEmision), n.NumDoc, n.Comentario,
                    n.TotalDoc ?? 0m, n.BaseTipo == TipoBaseFacturaCompra && n.BaseEntry != null));
            foreach (var p in pagos)
                renglones.Add(Abono(Fecha(p.FechaDoc, null), "Pago efectuado", p.NumDoc, p.Comentario, p.TotalPago));
            return renglones;
        }

        // Una nota de crédito sin factura base es solo informativa: se muestra pero no mueve el saldo.
        private static Renglon NotaCredito(string tipo, DateTime fecha, int numDoc, string? comentario, decimal total, bool tieneBase) =>
            tieneBase
                ? Abono(fecha, tipo, numDoc, comentario, total)
                : new Renglon(fecha, tipo, numDoc, Descripcion(comentario, tipo, numDoc), 0m, 0m, true);

        private static Renglon Cargo(DateTime fecha, string tipo, int numDoc, string? comentario, decimal monto) =>
            new(fecha, tipo, numDoc, Descripcion(comentario, tipo, numDoc), Redondear(monto), 0m, false);

        private static Renglon Abono(DateTime fecha, string tipo, int numDoc, string? comentario, decimal monto) =>
            new(fecha, tipo, numDoc, Descripcion(comentario, tipo, numDoc), 0m, Redondear(monto), false);

        // Fecha del documento sin hora; un documento sin fecha cae en el saldo inicial.
        private static DateTime Fecha(DateTime? fechaDoc, DateTime? fechaEmision) =>
            (fechaDoc ?? fechaEmision ?? DateTime.MinValue).Date;

        // Comentario breve del documento; si no tiene, su nombre ("Factura 123").
        private static string Descripcion(string? comentario, string tipo, int numDoc)
        {
            var texto = comentario?.Trim();
            if (string.IsNullOrEmpty(texto))
                return $"{tipo} {numDoc}";
            return texto.Length > LongitudMaximaDescripcion ? texto.Substring(0, LongitudMaximaDescripcion) : texto;
        }

        private static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
    }
}
