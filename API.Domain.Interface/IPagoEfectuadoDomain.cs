using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    /// <summary>Factura de compra o nota de débito de un proveedor con saldo por pagar.</summary>
    public sealed record DocumentoPorPagar(FacturaCompra Documento, SaldoFacturaCompra Saldo);

    public interface IPagoEfectuadoDomain
    {
        #region async methods
        /// <summary>Registra un pago aplicado a documentos del proveedor; devuelve el Entry. Lanza Exception con mensaje de usuario.</summary>
        Task<int> InsertarAsync(PagoEfectuado obj, IEnumerable<PagoEfectuadoFactura> facturas, IEnumerable<PagoEfectuadoMedio> medios);
        /// <summary>Cancela el pago (el saldo de los documentos se recupera solo). false si no existe.</summary>
        Task<bool> CancelarAsync(int id);
        Task<PagoEfectuado?> ObtenerAsync(int id);
        Task<IQueryable<PagoEfectuado>> ObtenerTodosAsync();
        Task<IEnumerable<PagoEfectuadoFactura>> ObtenerFacturasAsync(int id);
        Task<IEnumerable<PagoEfectuadoMedio>> ObtenerMediosAsync(int id);
        /// <summary>Facturas de compra y notas de débito del proveedor, no canceladas y con saldo mayor que 0, de la más antigua a la más reciente.</summary>
        Task<IReadOnlyList<DocumentoPorPagar>> ObtenerAbiertosAsync(string codigoSn);
        #endregion
    }
}
