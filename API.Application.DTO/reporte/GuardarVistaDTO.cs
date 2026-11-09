using System.Text.Json;

namespace API.Application.DTO.reporte
{
    public class GuardarVistaDTO
    {
        public string Nombre { get; set; } = "";
        public JsonElement Configuracion { get; set; }
    }
}
