using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    /// <summary>Factura o nota de débito de un cliente con saldo por cobrar.</summary>
    public sealed record DocumentoPorCobrar(Factura Documento, SaldoFactura Saldo);

    public interface IPagoRecibidoDomain
    {
        #region async methods
        /// <summary>Registra un pago aplicado a documentos del cliente; devuelve el Entry. Lanza Exception con mensaje de usuario.</summary>
        Task<int> InsertarAsync(PagoRecibido obj, IEnumerable<PagoRecibidoFactura> facturas, IEnumerable<PagoRecibidoMedio> medios);
        /// <summary>Cancela el pago (el saldo de los documentos se recupera solo). false si no existe.</summary>
        Task<bool> CancelarAsync(int id);
        Task<PagoRecibido?> ObtenerAsync(int id);
        Task<IQueryable<PagoRecibido>> ObtenerTodoAsync();
        Task<IEnumerable<PagoRecibidoFactura>> ObtenerFacturasAsync(int id);
        Task<IEnumerable<PagoRecibidoMedio>> ObtenerMediosAsync(int id);
        /// <summary>Facturas y notas de débito del cliente, no canceladas y con saldo mayor que 0, de la más antigua a la más reciente.</summary>
        Task<IReadOnlyList<DocumentoPorCobrar>> ObtenerDocumentosPorCobrarAsync(string codigoSn);
        #endregion
    }
}
