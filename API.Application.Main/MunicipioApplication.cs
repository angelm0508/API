using API.Application.DTO;
using API.Application.DTO.municipio;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;

namespace API.Application.Main
{
    public class MunicipioApplication : IMunicipioApplication
    {
        private readonly IMunicipioDomain _municipioDomain;
        private readonly IMapper _mapper;

        public MunicipioApplication(IMunicipioDomain municipioDomain, IMapper mapper)
        {
            _municipioDomain = municipioDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<bool>> InsertarAsync(MunicipioCrearDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var municipio = _mapper.Map<Municipio>(obj);
                respuesta.Dato = await _municipioDomain.InsertarAsync(municipio);
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

        public async Task<Respuesta<bool>> ActualizarAsync(string codigo, MunicipioActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var municipio = _mapper.Map<Municipio>(obj);
                respuesta.Dato = await _municipioDomain.ActualizarAsync(codigo, municipio);
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
                respuesta.Dato = await _municipioDomain.EliminarAsync(codigo);
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

        public async Task<Respuesta<MunicipioDTO>> ObtenerPorCodigoAsync(string codigo)
        {
            var respuesta = new Respuesta<MunicipioDTO>();
            try
            {
                var municipio = await _municipioDomain.ObtenerPorCodigoAsync(codigo);
                respuesta.Dato = _mapper.Map<MunicipioDTO>(municipio);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<MunicipioDTO>> ObtenerPorNombreAsync(string nombre)
        {
            var respuesta = new Respuesta<MunicipioDTO>();
            try
            {
                var municipio = await _municipioDomain.ObtenerPorNombreAsync(nombre);
                respuesta.Dato = _mapper.Map<MunicipioDTO>(municipio);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<MunicipioDTO>>> ObtenerAsync()
        {
            var respuesta = new Respuesta<IEnumerable<MunicipioDTO>>();
            try
            {
                var municipios = await _municipioDomain.ObtenerTodoAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<MunicipioDTO>>(municipios);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<MunicipioDTO>>> ObtenerContengaNombreAsync(string nombre)
        {
            var respuesta = new Respuesta<IEnumerable<MunicipioDTO>>();
            try
            {
                var municipios = await _municipioDomain.ObtenerContengaNombreAsync(nombre);
                respuesta.Dato = _mapper.Map<IEnumerable<MunicipioDTO>>(municipios);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<MunicipioDTO>>> ObtenerContengaCodigoAsync(string codigo)
        {
            var respuesta = new Respuesta<IEnumerable<MunicipioDTO>>();
            try
            {
                var municipios = await _municipioDomain.ObtenerContengaCodigoAsync(codigo);
                respuesta.Dato = _mapper.Map<IEnumerable<MunicipioDTO>>(municipios);
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
