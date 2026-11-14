using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core.Reportes.Compartido
{
    /// <summary>Una nota de crédito con su importe neto (sin IVA), positivo: es lo que se resta de la venta.</summary>
    public sealed record NotaCreditoNeta(DateTime? Fecha, string? CodigoSn, string? NombreSn, string? Moneda, decimal Importe);

    /// <summary>Lectura de las notas de crédito no canceladas para los reportes por período y por cliente (Ruling 7).</summary>
    public static class NotasCreditoNetas
    {
        /// <summary>Notas no canceladas con <c>FechaDoc</c> en [desde, hastaExclusiva) y del cliente indicado (si se pidió).</summary>
        public static async Task<IReadOnlyList<NotaCreditoNeta>> ObtenerAsync(
            IRepositorioGenerico<NotaCredito, int> notas, DateTime? desde, DateTime? hastaExclusiva, string? cliente)
        {
            var consulta = (await notas.ObtenerTodoAsync()).Where(n => n.Cancelado != "S");
            if (desde is { } d) consulta = consulta.Where(n => n.FechaDoc >= d);
            if (hastaExclusiva is { } h) consulta = consulta.Where(n => n.FechaDoc < h);
            if (cliente is { } c) consulta = consulta.Where(n => n.CodigoSn == c);

            var filas = await consulta
                .Select(n => new { n.FechaDoc, n.CodigoSn, n.NombreSn, n.MonedaDoc, n.TotalDoc, n.TotalImp })
                .ToListAsync();

            return filas
                .Select(n => new NotaCreditoNeta(n.FechaDoc, n.CodigoSn, n.NombreSn, n.MonedaDoc, (n.TotalDoc ?? 0m) - (n.TotalImp ?? 0m)))
                .ToList();
        }
    }
}
