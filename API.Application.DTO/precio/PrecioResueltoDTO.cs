namespace API.Application.DTO.precio
{
    public class PrecioResueltoDTO
    {
        public string Codigo { get; set; } = null!;
        public decimal PrecioBase { get; set; }
        public decimal FactorEfectivo { get; set; }
        public decimal PrecioLista { get; set; }
    }
}
