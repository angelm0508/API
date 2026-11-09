namespace API.Domain.Interface.Reportes
{
    /// <summary>Valor de un filtro. Valor sirve a los filtros simples; Desde y Hasta al de rango de fechas (yyyy-MM-dd).</summary>
    public sealed class FiltroValor
    {
        public string? Valor { get; set; }
        public string? Desde { get; set; }
        public string? Hasta { get; set; }
    }

    public sealed class OrdenReporte
    {
        public string Campo { get; set; } = "";
        public bool Descendente { get; set; }
    }

    public sealed class AgrupacionReporte
    {
        public string Campo { get; set; } = "";
        /// <summary>Solo para columnas de fecha; sin valor se agrupa por día.</summary>
        public ParteFecha? Parte { get; set; }
    }

    public sealed class SolicitudReporte
    {
        public Dictionary<string, FiltroValor> Filtros { get; set; } = new();
        public List<string> Columnas { get; set; } = new();
        public List<OrdenReporte> Orden { get; set; } = new();
        public List<AgrupacionReporte> Agrupar { get; set; } = new();
        public bool SoloResumen { get; set; }
    }

    public sealed class ExportarReporteRequest
    {
        public SolicitudReporte Solicitud { get; set; } = new();
        /// <summary>Imagen PNG del gráfico en base64 (con o sin el prefijo data:image/png;base64,). Máximo 2 MB.</summary>
        public string? GraficoPng { get; set; }
    }
}
