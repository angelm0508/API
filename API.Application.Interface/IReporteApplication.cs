using API.Application.DTO;
using API.Application.DTO.reporte;
using API.Domain.Interface.Reportes;

namespace API.Application.Interface
{
    public interface IReporteApplication
    {
        /// <summary>Reportes cuyo módulo de permiso cumple el predicado.</summary>
        Respuesta<List<ReporteResumenDTO>> ObtenerCatalogo(Func<string, bool> puedeVerModulo);

        /// <summary>Definición del reporte; Resultado=false con «El reporte 'x' no existe.» si no existe.</summary>
        Respuesta<ReporteDefinicion> ObtenerDefinicion(string codigo);

        Task<Respuesta<ResultadoReporte>> EjecutarAsync(string codigo, SolicitudReporte solicitud);

        /// <summary>Ejecuta el reporte con la solicitud y lo genera en el formato pedido (xlsx, pdf o csv).</summary>
        Task<Respuesta<ArchivoReporteDTO>> ExportarAsync(string codigo, ExportarReporteRequest request, string formato, string empresa, string usuario);
    }
}
