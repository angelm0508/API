using System.Text.Json;

namespace API.Application.DTO.reporte
{
    public class ReporteVistaDTO
    {
        public int Entry { get; set; }
        public string Nombre { get; set; } = "";
        /// <summary>Configuración guardada (filtros, columnas, orden, agrupación y gráfico) como objeto JSON.</summary>
        public JsonElement Configuracion { get; set; }
        public DateTime FechaActualizacion { get; set; }
    }
}
