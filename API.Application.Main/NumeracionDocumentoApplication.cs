using API.Application.DTO;
using API.Application.DTO.numeracionDocumento;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;

namespace API.Application.Main
{
    public class NumeracionDocumentoApplication : INumeracionDocumentoApplication
    {
        private readonly INumeracionDocumentoDomain _numeracionDomain;
        private readonly IMapper _mapper;

        public NumeracionDocumentoApplication(INumeracionDocumentoDomain numeracionDomain, IMapper mapper)
        {
            _numeracionDomain = numeracionDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<bool>> InsertarAsync(NumeracionDocumentoCrearDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var numeracion = _mapper.Map<NumeracionDocumento>(obj);
                respuesta.Dato = await _numeracionDomain.InsertarAsync(numeracion);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro agregado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = $"{ex.Message} / {ex.InnerException}";
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(string codigo, NumeracionDocumentoActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var numeracion = _mapper.Map<NumeracionDocumento>(obj);
                respuesta.Dato = await _numeracionDomain.ActualizarAsync(codigo, numeracion);
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

        public async Task<Respuesta<bool>> EliminarAsync(string codigo)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _numeracionDomain.EliminarAsync(codigo);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro eliminado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = $"{ex.Message} \n {ex.InnerException}";
            }
            return respuesta;
        }

        public async Task<Respuesta<NumeracionDocumentoDTO>> ObtenerPorCodigoAsync(string codigo)
        {
            var respuesta = new Respuesta<NumeracionDocumentoDTO>();
            try
            {
                var numeracion = await _numeracionDomain.ObtenerPorCodigoAsync(codigo);
                respuesta.Dato = _mapper.Map<NumeracionDocumentoDTO>(numeracion);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<NumeracionDocumentoDTO>>> ObtenerAsync()
        {
            var respuesta = new Respuesta<IEnumerable<NumeracionDocumentoDTO>>();
            try
            {
                var numeraciones = await _numeracionDomain.ObtenerTodoAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<NumeracionDocumentoDTO>>(numeraciones);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<NumeracionDocumentoDTO>>> ObtenerContengaCodigoAsync(string codigo)
        {
            var respuesta = new Respuesta<IEnumerable<NumeracionDocumentoDTO>>();
            try
            {
                var numeraciones = await _numeracionDomain.ObtenerContengaCodigoAsync(codigo);
                respuesta.Dato = _mapper.Map<IEnumerable<NumeracionDocumentoDTO>>(numeraciones);
                if (respuesta.Dato != null)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Consulta realizada correctamente.";
                }
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
