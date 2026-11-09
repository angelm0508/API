namespace API.Domain.Interface.Reportes
{
    public interface IReporte
    {
        ReporteDefinicion Definicion { get; }

        /// <summary>
        /// Devuelve a lo sumo "limite" filas, en el orden natural del reporte. El motor pide el tope + 1 para detectar el
        /// truncado. Cada fila trae TODAS las columnas de la definición (clave = Campo).
        /// </summary>
        Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite);
    }

    public interface IMotorReportes
    {
        Task<ResultadoReporte> EjecutarAsync(IReporte reporte, SolicitudReporte solicitud);
    }

    public interface ICatalogoReportes
    {
        IReadOnlyList<IReporte> Todos { get; }
        IReporte? Buscar(string codigo);
    }

    public sealed record ContextoExportacion(string Empresa, string Usuario, DateTime Fecha, string FiltrosTexto, byte[]? GraficoPng);

    public interface IExportadorReporte
    {
        /// <summary>"xlsx", "pdf" o "csv".</summary>
        string Formato { get; }
        string ContentType { get; }
        string Extension { get; }
        byte[] Exportar(ResultadoReporte resultado, ReporteDefinicion definicion, ContextoExportacion contexto);
    }

    public sealed record ReporteVistaDato(int Entry, string Nombre, string Configuracion, DateTime FechaActualizacion);

    public interface IReporteVistaDomain
    {
        Task<IReadOnlyList<ReporteVistaDato>> ObtenerAsync(int usuarioId, string codigoReporte);

        /// <summary>Crea la vista o, si el usuario ya tiene una con ese nombre para ese reporte, la reemplaza.</summary>
        Task<ReporteVistaDato> GuardarAsync(int usuarioId, string codigoReporte, string nombre, string configuracionJson);

        /// <summary>false si la vista no existe o no es del usuario.</summary>
        Task<bool> EliminarAsync(int usuarioId, string codigoReporte, int entry);
    }
}
