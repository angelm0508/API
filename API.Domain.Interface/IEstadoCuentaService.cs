using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    /// <summary>Renglón del estado de cuenta. Los documentos informativos (nota de crédito sin factura base) llevan Cargo y Abono en cero.</summary>
    public sealed record MovimientoCuenta(DateTime Fecha, string Tipo, int NumDoc, string Descripcion, decimal Cargo, decimal Abono, decimal SaldoAcumulado, bool Informativo);

    public sealed record EstadoCuenta(SocioNegocio Socio, DateTime Desde, DateTime Hasta, decimal SaldoInicial, IReadOnlyList<MovimientoCuenta> Movimientos, decimal SaldoFinal, decimal SaldoActual, decimal Vencido);

    public interface IEstadoCuentaService
    {
        /// <summary>desde/hasta nulos = 1 de enero del año en curso / hoy. Lanza "El socio de negocio no existe." o "La fecha inicial no puede ser posterior a la final.".</summary>
        Task<EstadoCuenta> ObtenerAsync(string codigoSn, DateTime? desde, DateTime? hasta);
    }
}
