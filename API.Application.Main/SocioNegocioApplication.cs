using API.Application.DTO;
using API.Application.DTO.socioNegocio;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;

namespace API.Application.Main
{
    public class SocioNegocioApplication : ISocioNegocioApplication
    {
        private readonly ISocioNegocioDomain _socioNegocioDomain;
        private readonly IMapper _mapper;

        public SocioNegocioApplication(ISocioNegocioDomain socioNegocioDomain, IMapper mapper)
        {
            _socioNegocioDomain = socioNegocioDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<string>> InsertarAsync(SocioNegocioCrearDTO obj)
        {
            var respuesta = new Respuesta<string>();
            try
            {
                var socioNegocio = _mapper.Map<SocioNegocio>(obj);
                respuesta.Dato = await _socioNegocioDomain.InsertarAsync(socioNegocio);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = $"{ex.Message} / {ex.InnerException}";
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(string codigo, SocioNegocioActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var socioNegocio = _mapper.Map<SocioNegocio>(obj);
                respuesta.Dato = await _socioNegocioDomain.ActualizarAsync(codigo, socioNegocio);
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
                respuesta.Dato = await _socioNegocioDomain.EliminarAsync(codigo);
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

        public async Task<Respuesta<SocioNegocioDTO>> ObtenerPorCodigoAsync(string codigo)
        {
            var respuesta = new Respuesta<SocioNegocioDTO>();
            try
            {
                var socioNegocio = await _socioNegocioDomain.ObtenerPorCodigoAsync(codigo);
                respuesta.Dato = _mapper.Map<SocioNegocioDTO>(socioNegocio);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<SocioNegocioDTO>> ObtenerPorNombreAsync(string nombre)
        {
            var respuesta = new Respuesta<SocioNegocioDTO>();
            try
            {
                var socioNegocio = await _socioNegocioDomain.ObtenerPorNombreAsync(nombre);
                respuesta.Dato = _mapper.Map<SocioNegocioDTO>(socioNegocio);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<SocioNegocioDTO>>> ObtenerAsync(string? tipo = null)
        {
            var respuesta = new Respuesta<IEnumerable<SocioNegocioDTO>>();
            try
            {
                var sociosNegocios = await _socioNegocioDomain.ObtenerTodoAsync(tipo);
                respuesta.Dato = _mapper.Map<IEnumerable<SocioNegocioDTO>>(sociosNegocios);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<SocioNegocioDTO>>> ObtenerContengaNombreAsync(string nombre, string? tipo = null)
        {
            var respuesta = new Respuesta<IEnumerable<SocioNegocioDTO>>();
            try
            {
                var sociosNegocios = await _socioNegocioDomain.ObtenerContengaNombreAsync(nombre, tipo);
                respuesta.Dato = _mapper.Map<IEnumerable<SocioNegocioDTO>>(sociosNegocios);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<SocioNegocioDTO>>> ObtenerContengaCodigoAsync(string codigo)
        {
            var respuesta = new Respuesta<IEnumerable<SocioNegocioDTO>>();
            try
            {
                var sociosNegocios = await _socioNegocioDomain.ObtenerContengaCodigoAsync(codigo);
                respuesta.Dato = _mapper.Map<IEnumerable<SocioNegocioDTO>>(sociosNegocios);
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
