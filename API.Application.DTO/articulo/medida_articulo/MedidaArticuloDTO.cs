namespace API.Application.DTO.articulo.medida_articulo
{
    public class MedidaArticuloDTO
    {
        public int Entry { get; set; }
        public string Codigo { get; set; }
        public string? Nombre { get; set; }
        public decimal? Largo { get; set; }
        public decimal? Ancho { get; set; }
        public decimal? Altura { get; set; }
        public decimal? Volumen { get; set; }
        public decimal? Peso { get; set; }
        public string? Bloqueado { get; set; }
    }
}
