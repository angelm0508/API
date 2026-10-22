namespace API.Application.DTO.ordenServicio
{
    public class OrdenServicioActualizarDTO
    {
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaIngreso { get; set; }
        public DateTime? FechaPromesa { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int MotoEntry { get; set; }
        public string? CodMecanico { get; set; }
        public int? KmIngreso { get; set; }
        public string? FallaReportada { get; set; }
        public string? Diagnostico { get; set; }
        public string? Observaciones { get; set; }
        public string? Comentario { get; set; }
    }
}
