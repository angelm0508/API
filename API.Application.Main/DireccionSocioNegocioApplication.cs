using API.Application.DTO;
using API.Application.DTO.direccionSocioNegocio;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;

namespace API.Application.Main
{
    public class DireccionSocioNegocioApplication : IDireccionSocioNegocioApplication
    {
        private readonly IDireccionSocioNegocioDomain _direccionDomain;
        private readonly IMapper _mapper;

        public DireccionSocioNegocioApplication(IDireccionSocioNegocioDomain direccionDomain, IMapper mapper)
        {
            _direccionDomain = direccionDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<bool>> InsertarAsync(DireccionSocioNegocioCrearDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var direccion = _mapper.Map<DireccionSocioNegocio>(obj);
                respuesta.Dato = await _direccionDomain.InsertarAsync(direccion);
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

        public async Task<Respuesta<bool>> ActualizarAsync(string codigo, DireccionSocioNegocioActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var direccion = _mapper.Map<DireccionSocioNegocio>(obj);
                respuesta.Dato = await _direccionDomain.ActualizarAsync(codigo, direccion);
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
                respuesta.Dato = await _direccionDomain.EliminarAsync(codigo);
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

        public async Task<Respuesta<DireccionSocioNegocioDTO>> ObtenerPorCodigoAsync(string codigo)
        {
            var respuesta = new Respuesta<DireccionSocioNegocioDTO>();
            try
            {
                var direccion = await _direccionDomain.ObtenerPorCodigoAsync(codigo);
                respuesta.Dato = _mapper.Map<DireccionSocioNegocioDTO>(direccion);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<DireccionSocioNegocioDTO>>> ObtenerAsync()
        {
            var respuesta = new Respuesta<IEnumerable<DireccionSocioNegocioDTO>>();
            try
            {
                var direcciones = await _direccionDomain.ObtenerTodoAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<DireccionSocioNegocioDTO>>(direcciones);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<DireccionSocioNegocioDTO>>> ObtenerContengaCodigoAsync(string codigo)
        {
            var respuesta = new Respuesta<IEnumerable<DireccionSocioNegocioDTO>>();
            try
            {
                var direcciones = await _direccionDomain.ObtenerContengaCodigoAsync(codigo);
                respuesta.Dato = _mapper.Map<IEnumerable<DireccionSocioNegocioDTO>>(direcciones);
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
