namespace API.Domain.Interface
{
    /// <summary>Saldo de un socio de negocio repartido por antigüedad de vencimiento (al día de hoy).</summary>
    public sealed record FilaAntiguedad(string CodigoSn, string NombreSn, decimal Vigente, decimal De1a30, decimal De31a60, decimal De61a90, decimal Mas90, decimal Total);

    /// <summary>Documento abierto de un socio con sus días de vencido (negativo o cero = aún vigente).</summary>
    public sealed record DocumentoAntiguedad(int Entry, string Tipo, int NumDoc, DateTime FechaDoc, DateTime Vencimiento, int DiasVencido, decimal Saldo);

    public interface IAntiguedadService
    {
        /// <summary>tipo "C" (por cobrar) o "P" (por pagar); otro valor lanza "El tipo debe ser C o P.". Al día de hoy.</summary>
        Task<IReadOnlyList<FilaAntiguedad>> ObtenerAsync(string tipo);

        /// <summary>Documentos abiertos del socio, del vencimiento más antiguo al más reciente; vacío si el socio no existe o es del otro tipo.</summary>
        Task<IReadOnlyList<DocumentoAntiguedad>> ObtenerDetalleAsync(string tipo, string codigoSn);
    }
}
