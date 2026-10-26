namespace API.Domain.Interface
{
    /// <summary>
    /// Resuelve la condición de pago y la fecha de vencimiento de una factura o nota de débito al crearla.
    /// </summary>
    public interface IResolutorVencimiento
    {
        /// <summary>
        /// condición = pedida, o la del socio, o "CON"; debe existir y estar activa (si no, lanza el mensaje de
        /// <see cref="ICondicionPagoDomain.ObtenerActivaAsync"/>). Vencimiento = fechaDoc.Date + días de la condición
        /// (con fechaDoc nula se usa la fecha de hoy solo para este cálculo).
        /// </summary>
        Task<(string Condicion, DateTime Vencimiento)> ResolverAsync(string? condicionPedida, string? condicionDelSocio, DateTime? fechaDoc);
    }
}
