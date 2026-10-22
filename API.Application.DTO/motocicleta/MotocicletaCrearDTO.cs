namespace API.Application.DTO.motocicleta
{
    public class MotocicletaCrearDTO
    {
        public string CodigoSn { get; set; } = null!;
        public string Placa { get; set; } = null!;
        public string? Marca { get; set; }
        public string? Modelo { get; set; }
        public short? Anio { get; set; }
        public string? Color { get; set; }
        public string? Vin { get; set; }
        public string? Comentario { get; set; }
        public string? Activo { get; set; }
    }
}
