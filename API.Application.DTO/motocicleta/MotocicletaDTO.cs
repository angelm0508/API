namespace API.Application.DTO.motocicleta
{
    public class MotocicletaDTO
    {
        public int Entry { get; set; }
        public string CodigoSn { get; set; } = null!;
        public string? NombreSn { get; set; }
        public string Placa { get; set; } = null!;
        public string? Marca { get; set; }
        public string? Modelo { get; set; }
        public short? Anio { get; set; }
        public string? Color { get; set; }
        public string? Vin { get; set; }
        public string? Comentario { get; set; }
        public string Activo { get; set; } = null!;
    }
}
