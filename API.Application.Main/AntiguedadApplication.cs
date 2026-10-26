using API.Application.DTO;
using API.Application.DTO.antiguedad;
using API.Application.Interface;
using API.Domain.Interface;

namespace API.Application.Main
{
    public class AntiguedadApplication : IAntiguedadApplication
    {
        private const string TipoCobrar = "C";
        private const string TipoPagar = "P";

        private readonly IAntiguedadService _antiguedad;

        public AntiguedadApplication(IAntiguedadService antiguedad)
        {
            _antiguedad = antiguedad;
        }

        #region async methods
        public Task<Respuesta<List<FilaAntiguedadDTO>>> ObtenerCobrarAsync() => ObtenerAsync(TipoCobrar);

        public Task<Respuesta<List<FilaAntiguedadDTO>>> ObtenerPagarAsync() => ObtenerAsync(TipoPagar);

        public Task<Respuesta<List<DocumentoAntiguedadDTO>>> ObtenerDetalleCobrarAsync(string codigoSn) => ObtenerDetalleAsync(TipoCobrar, codigoSn);

        public Task<Respuesta<List<DocumentoAntiguedadDTO>>> ObtenerDetallePagarAsync(string codigoSn) => ObtenerDetalleAsync(TipoPagar, codigoSn);
        #endregion

        private async Task<Respuesta<List<FilaAntiguedadDTO>>> ObtenerAsync(string tipo)
        {
            var respuesta = new Respuesta<List<FilaAntiguedadDTO>>();
            try
            {
                var filas = await _antiguedad.ObtenerAsync(tipo);
                respuesta.Dato = filas.Select(f => new FilaAntiguedadDTO
                {
                    CodigoSn = f.CodigoSn,
                    NombreSn = f.NombreSn,
                    Vigente = f.Vigente,
                    De1a30 = f.De1a30,
                    De31a60 = f.De31a60,
                    De61a90 = f.De61a90,
                    Mas90 = f.Mas90,
                    Total = f.Total
                }).ToList();
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        private async Task<Respuesta<List<DocumentoAntiguedadDTO>>> ObtenerDetalleAsync(string tipo, string codigoSn)
        {
            var respuesta = new Respuesta<List<DocumentoAntiguedadDTO>>();
            try
            {
                var documentos = await _antiguedad.ObtenerDetalleAsync(tipo, codigoSn);
                respuesta.Dato = documentos.Select(d => new DocumentoAntiguedadDTO
                {
                    Entry = d.Entry,
                    Tipo = d.Tipo,
                    NumDoc = d.NumDoc,
                    FechaDoc = d.FechaDoc,
                    Vencimiento = d.Vencimiento,
                    DiasVencido = d.DiasVencido,
                    Saldo = d.Saldo
                }).ToList();
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
