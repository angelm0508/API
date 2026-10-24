namespace API.Application.DTO.pagoRecibido
{
    public class PagoRecibidoFacturaCrearDTO
    {
        /// <summary>Entry de la factura o nota de débito de cliente.</summary>
        public int FacturaEntry { get; set; }
        public decimal MontoAplicado { get; set; }
    }
}
