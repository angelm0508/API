namespace API.Application.DTO.inventario
{
    public class ExistenciaArticuloDTO
    {
        public string CodArticulo { get; set; } = null!;
        public string CodAlmacen { get; set; } = null!;
        public decimal Disponible { get; set; }
        public decimal Comprometido { get; set; }
        public decimal Pedido { get; set; }
        public DateTime FechaActualizacion { get; set; }
    }
}
