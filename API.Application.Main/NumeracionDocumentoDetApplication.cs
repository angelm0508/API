using API.Application.DTO;
using API.Application.DTO.numeracion.numeracion_documento_det;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class NumeracionDocumentoDetApplication : INumeracionDocumentoDetApplication
    {
        private readonly INumeracionDocumentoDetDomain _numeracionDocumentoDetDomain;
        private readonly IMapper _mapper;

        public NumeracionDocumentoDetApplication(INumeracionDocumentoDetDomain numeracionDocumentoDetDomain, IMapper mapper)
        {
            _numeracionDocumentoDetDomain = numeracionDocumentoDetDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(NumeracionDocumentoDetCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var numeracionDoc = _mapper.Map<NumeracionDocumentoDet>(obj);
                respuesta.Dato = await _numeracionDocumentoDetDomain.InsertarAsync(numeracionDoc);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int serie, NumeracionDocumentoDetActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var numeracionDoc = _mapper.Map<NumeracionDocumentoDet>(obj);
                respuesta.Dato = await _numeracionDocumentoDetDomain.ActualizarAsync(serie, numeracionDoc);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro actualizado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> EliminarAsync(int serie)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _numeracionDocumentoDetDomain.EliminarAsync(serie);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro eliminado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<NumeracionDocumentoDetDTO>> ObtenerAsync(int serie)
        {
            var respuesta = new Respuesta<NumeracionDocumentoDetDTO>();
            try
            {
                var numeracionDoc = await _numeracionDocumentoDetDomain.ObtenerAsync(serie);
                respuesta.Dato = _mapper.Map<NumeracionDocumentoDetDTO>(numeracionDoc);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<NumeracionDocumentoDetDTO>>> ObtenerPorDocumentoAsync(string codigoObj)
        {
            var respuesta = new Respuesta<IEnumerable<NumeracionDocumentoDetDTO>>();
            try
            {
                var numeracionDocs = await _numeracionDocumentoDetDomain.ObtenerPorDocumentoAsync(codigoObj);
                respuesta.Dato = _mapper.Map<IEnumerable<NumeracionDocumentoDetDTO>>(numeracionDocs);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<NumeracionDocumentoDetDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<NumeracionDocumentoDetDTO>>();
            try
            {
                var queryable = await _numeracionDocumentoDetDomain.ObtenerTodoAsync();
                var numeracionDocs = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<NumeracionDocumentoDetDTO>>(numeracionDocs);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<string>> GenerarCodigoAsync(int serie)
        {
            var respuesta = new Respuesta<string>();
            try
            {
                respuesta.Dato = await _numeracionDocumentoDetDomain.GenerarCodigoAsync(serie);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Código generado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
        #endregion
    }
}
