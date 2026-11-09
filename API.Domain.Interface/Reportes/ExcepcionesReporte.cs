namespace API.Domain.Interface.Reportes
{
    /// <summary>Solicitud de reporte inválida (filtro, columna, orden o agrupación no permitidos, vista inválida, etc.).</summary>
    public sealed class ReporteInvalidoException : Exception
    {
        public ReporteInvalidoException(string mensaje) : base(mensaje) { }
    }

    public sealed class ReporteNoExisteException : Exception
    {
        public ReporteNoExisteException(string codigo) : base($"El reporte '{codigo}' no existe.") { }
    }
}
