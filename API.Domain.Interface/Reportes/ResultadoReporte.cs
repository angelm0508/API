namespace API.Domain.Interface.Reportes
{
    /// <summary>Una fila de un reporte: campo → valor. Las filas de un reporte dinámico no tienen forma fija.</summary>
    public sealed class FilaReporte : Dictionary<string, object?> { }

    public sealed class GrupoReporte
    {
        public string Campo { get; set; } = "";
        public string Titulo { get; set; } = "";
        public string Clave { get; set; } = "";
        public string Etiqueta { get; set; } = "";
        public int Conteo { get; set; }
        public Dictionary<string, decimal?> Subtotales { get; set; } = new();
        public List<GrupoReporte> Hijos { get; set; } = new();
        public List<FilaReporte> Filas { get; set; } = new();
    }

    public sealed class ResultadoReporte
    {
        public List<ColumnaReporte> Columnas { get; set; } = new();
        /// <summary>Filas de detalle cuando no hay agrupación.</summary>
        public List<FilaReporte> Filas { get; set; } = new();
        /// <summary>Árbol de grupos cuando hay agrupación (las filas de detalle cuelgan de las hojas, salvo SoloResumen).</summary>
        public List<GrupoReporte> Grupos { get; set; } = new();
        public Dictionary<string, decimal?> Totales { get; set; } = new();
        public bool Truncado { get; set; }
        /// <summary>Filas que quedaron en el resultado (como máximo el tope del motor).</summary>
        public int TotalFilas { get; set; }
        public List<string> Avisos { get; set; } = new();
    }
}
