namespace API.Application.DTO.salidaMercancia
{
    public class SalidaMercanciaDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public int Serie { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? EstadoInv { get; set; }
        public string? Cancelado { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaContab { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? Referencia { get; set; }
        public string? Comentario { get; set; }
        public decimal? TotalDoc { get; set; }
    }
}
