namespace API.Application.DTO.reporte
{
    public class ArchivoReporteDTO
    {
        public byte[] Contenido { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "application/octet-stream";
        public string NombreArchivo { get; set; } = "reporte";
    }
}
