namespace API.Domain.Interface
{
    /// <summary>Saldo abierto y vencido de un socio de negocio (cliente: por cobrar; proveedor: por pagar).</summary>
    public sealed record SaldoSocio(string CodigoSn, string Tipo, decimal Saldo, decimal Vencido, int DocumentosAbiertos);

    public interface ISaldoSocioService
    {
        /// <summary>Saldo y vencido de cada socio pedido (solo aparecen los que existen); el tipo sale de SocioNegocio.TipoSn.</summary>
        Task<IReadOnlyDictionary<string, SaldoSocio>> ObtenerAsync(IEnumerable<string> codigosSn);
    }
}
