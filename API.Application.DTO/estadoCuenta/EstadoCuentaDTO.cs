namespace API.Application.DTO.estadoCuenta
{
    /// <summary>Estado de cuenta de un socio de negocio (cliente o proveedor) en un rango de fechas.</summary>
    public class EstadoCuentaDTO
    {
        public string CodigoSn { get; set; } = null!;
        public string? NombreSn { get; set; }
        /// <summary>C = cliente, P = proveedor.</summary>
        public string? TipoSn { get; set; }
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        public decimal SaldoInicial { get; set; }
        public decimal SaldoFinal { get; set; }
        public decimal SaldoActual { get; set; }
        public decimal Vencido { get; set; }
        public List<MovimientoCuentaDTO> Movimientos { get; set; } = new();
    }
}
