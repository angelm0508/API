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
        public async Task<Respuesta<string>> InsertarAsync(NumeracionDocumentoDetCrearDTO obj)
        {
            var respuesta = new Respuesta<string>();
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

        public async Task<Respuesta<bool>> ActualizarAsync(string codigoObj, NumeracionDocumentoDetActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var numeracionDoc = _mapper.Map<NumeracionDocumentoDet>(obj);
                respuesta.Dato = await _numeracionDocumentoDetDomain.ActualizarAsync(codigoObj, numeracionDoc);
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

        public async Task<Respuesta<bool>> EliminarAsync(string codigoObj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _numeracionDocumentoDetDomain.EliminarAsync(codigoObj);
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

        public async Task<Respuesta<NumeracionDocumentoDetDTO>> ObtenerAsync(string codigoObj)
        {
            var respuesta = new Respuesta<NumeracionDocumentoDetDTO>();
            try
            {
                var numeracionDoc = await _numeracionDocumentoDetDomain.ObtenerAsync(codigoObj);
                respuesta.Dato = _mapper.Map<NumeracionDocumentoDetDTO>(numeracionDoc);
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
        #endregion
    }
}
