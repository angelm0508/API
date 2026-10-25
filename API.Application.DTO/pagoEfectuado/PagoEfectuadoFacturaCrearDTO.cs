namespace API.Application.DTO.pagoEfectuado
{
    public class PagoEfectuadoFacturaCrearDTO
    {
        /// <summary>Entry de la factura o nota de débito de proveedor.</summary>
        public int FacturaCompraEntry { get; set; }
        public decimal MontoAplicado { get; set; }
    }
}
