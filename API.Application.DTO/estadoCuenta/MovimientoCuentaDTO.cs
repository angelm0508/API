namespace API.Application.DTO.estadoCuenta
{
    /// <summary>Renglón del estado de cuenta. Los documentos informativos llevan Cargo y Abono en cero y no afectan el acumulado.</summary>
    public class MovimientoCuentaDTO
    {
        public DateTime Fecha { get; set; }
        public string Tipo { get; set; } = null!;
        public int NumDoc { get; set; }
        public string Descripcion { get; set; } = null!;
        public decimal Cargo { get; set; }
        public decimal Abono { get; set; }
        public decimal SaldoAcumulado { get; set; }
        public bool Informativo { get; set; }
    }
}
