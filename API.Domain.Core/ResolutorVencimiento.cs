using API.Domain.Entity.Models;
using API.Domain.Interface;

namespace API.Domain.Core
{
    /// <summary>
    /// Resuelve la condición de pago y la fecha de vencimiento de una factura o nota de débito al crearla:
    /// condición pedida, o la del socio, o CON (contado). Compartido por los cuatro dominios de inserción.
    /// </summary>
    public sealed class ResolutorVencimiento : IResolutorVencimiento
    {
        public const string CondicionPorDefecto = "CON";
        private readonly ICondicionPagoDomain _condiciones;

        public ResolutorVencimiento(ICondicionPagoDomain condiciones) => _condiciones = condiciones;

        public async Task<(string Condicion, DateTime Vencimiento)> ResolverAsync(string? condicionPedida, string? condicionDelSocio, DateTime? fechaDoc)
        {
            var codigo = FirstNonEmpty(condicionPedida, condicionDelSocio) ?? CondicionPorDefecto;
            var condicion = await _condiciones.ObtenerActivaAsync(codigo);   // lanza "no existe" / "está inactiva"
            var baseFecha = (fechaDoc ?? DateTime.Today).Date;
            return (condicion!.Codigo, baseFecha.AddDays(condicion.Dias));
        }

        private static string? FirstNonEmpty(params string?[] valores) =>
            valores.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrEmpty(v));
    }
}
