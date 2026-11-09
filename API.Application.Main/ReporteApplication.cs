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

        public ReporteApplication(ICatalogoReportes catalogo, IMotorReportes motor)
        {
            _catalogo = catalogo;
            _motor = motor;
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
    }
}
