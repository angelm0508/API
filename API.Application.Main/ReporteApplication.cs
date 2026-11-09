using API.Application.DTO;
using API.Application.DTO.reporte;
using API.Application.Interface;
using API.Domain.Interface.Reportes;

namespace API.Application.Main
{
    public class ReporteApplication : IReporteApplication
    {
        private readonly ICatalogoReportes _catalogo;
        private readonly IMotorReportes _motor;

        private readonly IEnumerable<IExportadorReporte> _exportadores;

        public ReporteApplication(ICatalogoReportes catalogo, IMotorReportes motor, IEnumerable<IExportadorReporte> exportadores)
        {
            _catalogo = catalogo;
            _motor = motor;
            _exportadores = exportadores;
        }

        public Respuesta<List<ReporteResumenDTO>> ObtenerCatalogo(Func<string, bool> puedeVerModulo)
        {
            var respuesta = new Respuesta<List<ReporteResumenDTO>>();
            try
            {
                respuesta.Dato = _catalogo.Todos
                    .Where(r => puedeVerModulo(r.Definicion.Modulo))
                    .Select(r => new ReporteResumenDTO
                    {
                        Codigo = r.Definicion.Codigo,
                        Nombre = r.Definicion.Nombre,
                        Descripcion = r.Definicion.Descripcion,
                        Familia = r.Definicion.Familia
                    })
                    .ToList();
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public Respuesta<ReporteDefinicion> ObtenerDefinicion(string codigo)
        {
            var respuesta = new Respuesta<ReporteDefinicion>();
            var reporte = _catalogo.Buscar(codigo);
            if (reporte is null)
            {
                respuesta.Mensaje = new ReporteNoExisteException(codigo).Message;
                return respuesta;
            }

            respuesta.Dato = reporte.Definicion;
            respuesta.Resultado = true;
            respuesta.Mensaje = "Consulta realizada correctamente.";
            return respuesta;
        }

        public async Task<Respuesta<ResultadoReporte>> EjecutarAsync(string codigo, SolicitudReporte solicitud)
        {
            var respuesta = new Respuesta<ResultadoReporte>();
            try
            {
                var reporte = _catalogo.Buscar(codigo) ?? throw new ReporteNoExisteException(codigo);
                respuesta.Dato = await _motor.EjecutarAsync(reporte, solicitud);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        private const int MaxGraficoBytes = 2 * 1024 * 1024;

        public async Task<Respuesta<ArchivoReporteDTO>> ExportarAsync(string codigo, ExportarReporteRequest request, string formato, string empresa, string usuario)
        {
            var respuesta = new Respuesta<ArchivoReporteDTO>();
            try
            {
                if (request?.Solicitud is null)
                    throw new ReporteInvalidoException("La solicitud del reporte es obligatoria.");

                var reporte = _catalogo.Buscar(codigo) ?? throw new ReporteNoExisteException(codigo);
                var exportador = _exportadores.FirstOrDefault(e => string.Equals(e.Formato, formato, StringComparison.OrdinalIgnoreCase))
                    ?? throw new ReporteInvalidoException($"El formato '{formato}' no es válido. Use xlsx, pdf o csv.");
                var grafico = DecodificarGrafico(request.GraficoPng);

                var resultado = await _motor.EjecutarAsync(reporte, request.Solicitud);

                var ahora = DateTime.Now;
                var contexto = new ContextoExportacion(empresa, usuario, ahora, TextoFiltros(reporte.Definicion, request.Solicitud), grafico);
                respuesta.Dato = new ArchivoReporteDTO
                {
                    Contenido = exportador.Exportar(resultado, reporte.Definicion, contexto),
                    ContentType = exportador.ContentType,
                    NombreArchivo = $"{reporte.Definicion.Codigo}-{ahora:yyyyMMdd-HHmm}.{exportador.Extension}"
                };
                respuesta.Resultado = true;
                respuesta.Mensaje = "Archivo generado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        private static byte[]? DecodificarGrafico(string? base64)
        {
            if (string.IsNullOrWhiteSpace(base64)) return null;

            var texto = base64.Trim();
            var coma = texto.IndexOf(',');
            if (texto.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && coma >= 0) texto = texto[(coma + 1)..];

            var demasiadoGrande = new ReporteInvalidoException("El gráfico es demasiado grande (máximo 2 MB).");
            if (texto.Length > MaxGraficoBytes / 3 * 4 + 8) throw demasiadoGrande;

            byte[] bytes;
            try { bytes = Convert.FromBase64String(texto); }
            catch (FormatException) { throw new ReporteInvalidoException("El gráfico no es una imagen PNG válida."); }

            if (bytes.Length > MaxGraficoBytes) throw demasiadoGrande;
            if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47)
                throw new ReporteInvalidoException("El gráfico no es una imagen PNG válida.");
            return bytes;
        }

        private static string TextoFiltros(ReporteDefinicion definicion, SolicitudReporte solicitud)
        {
            var partes = new List<string>();
            foreach (var filtro in definicion.Filtros)
            {
                if (solicitud.Filtros is null || !solicitud.Filtros.TryGetValue(filtro.Campo, out var v) || v is null) continue;

                if (filtro.Tipo == TipoFiltro.RangoFechas)
                {
                    var desde = v.Desde?.Trim();
                    var hasta = v.Hasta?.Trim();
                    if (!string.IsNullOrEmpty(desde) && !string.IsNullOrEmpty(hasta)) partes.Add($"{filtro.Titulo}: {desde} a {hasta}");
                    else if (!string.IsNullOrEmpty(desde)) partes.Add($"{filtro.Titulo}: desde {desde}");
                    else if (!string.IsNullOrEmpty(hasta)) partes.Add($"{filtro.Titulo}: hasta {hasta}");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(v.Valor)) continue;
                var texto = filtro.Tipo == TipoFiltro.Lista
                    ? filtro.Opciones?.FirstOrDefault(o => o.Valor == v.Valor)?.Titulo ?? v.Valor
                    : v.Valor.Trim();
                partes.Add($"{filtro.Titulo}: {texto}");
            }
            return partes.Count == 0 ? "Sin filtros" : string.Join("; ", partes);
        }
    }
}
