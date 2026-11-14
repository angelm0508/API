using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core.Reportes.Compartido
{
    /// <summary>Una nota de débito por concepto con su importe neto (sin IVA), positivo: es lo que se suma a la venta.</summary>
    public sealed record NotaDebitoConceptoNeta(int Entry, DateTime? Fecha, string? CodigoSn, string? NombreSn, string? Moneda, decimal Importe);

    /// <summary>
    /// Lectura de las notas de débito POR CONCEPTO no canceladas (<c>Factura</c> con <c>DocSubType = "ND"</c> y <c>DocType = "S"</c>,
    /// sin líneas de artículo) para los reportes por período y por cliente. Las notas por artículos ya entran por
    /// <see cref="ConsultaLineasVenta"/>. Simétrico a <see cref="NotasCreditoNetas"/>.
    /// </summary>
    public static class NotasDebitoConceptoNetas
    {
        private const string SubTipoNotaDebito = "ND";
        private const string ModoConcepto = "S";

        /// <summary>Notas no canceladas con <c>FechaDoc</c> en [desde, hastaExclusiva) y del cliente indicado (si se pidió).</summary>
        public static async Task<IReadOnlyList<NotaDebitoConceptoNeta>> ObtenerAsync(
            IRepositorioGenerico<Factura, int> facturas, DateTime? desde, DateTime? hastaExclusiva, string? cliente)
        {
            var consulta = (await facturas.ObtenerTodoAsync())
                .Where(f => f.DocSubType == SubTipoNotaDebito && f.DocType == ModoConcepto && f.Cancelado != "S");
            if (desde is { } d) consulta = consulta.Where(f => f.FechaDoc >= d);
            if (hastaExclusiva is { } h) consulta = consulta.Where(f => f.FechaDoc < h);
            if (cliente is { } c) consulta = consulta.Where(f => f.CodigoSn == c);

            var filas = await consulta
                .Select(f => new { f.Entry, f.FechaDoc, f.CodigoSn, f.NombreSn, f.MonedaDoc, f.TotalDoc, f.TotalImp })
                .ToListAsync();

            return filas
                .Select(f => new NotaDebitoConceptoNeta(f.Entry, f.FechaDoc, f.CodigoSn, f.NombreSn, f.MonedaDoc, (f.TotalDoc ?? 0m) - (f.TotalImp ?? 0m)))
                .ToList();
        }
    }
}
