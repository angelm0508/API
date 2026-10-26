namespace API.Application.DTO.estadoCuenta
{
    /// <summary>Saldo abierto y vencido de un socio de negocio (cliente: por cobrar; proveedor: por pagar).</summary>
    public class SaldoSocioDTO
    {
        public string CodigoSn { get; set; } = null!;
        /// <summary>C = cliente, P = proveedor.</summary>
        public string Tipo { get; set; } = null!;
        public decimal Saldo { get; set; }
        public decimal Vencido { get; set; }
        public int DocumentosAbiertos { get; set; }
    }
}
