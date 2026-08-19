using API.Application.DTO;
using API.Application.DTO.pais;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;

namespace API.Application.Main
{
    public class PaisApplication : IPaisApplication
    {
        private readonly IPaisDomain _paisDomain;
        private readonly IMapper _mapper;

        public PaisApplication(IPaisDomain paisDomain, IMapper mapper)
        {
            _paisDomain = paisDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<bool>> InsertarAsync(PaisCrearDTO obj)
        {
            var respuesta = new Respuesta<bool>();

            try
            {
                var pais = _mapper.Map<Pai>(obj);

                respuesta.Dato = await _paisDomain
                                            .InsertarAsync(pais);

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
        public async Task<Respuesta<bool>> ActualizarAsync(string codigo, PaisActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var pais = _mapper.Map<Pai>(obj);
                respuesta.Dato = await _paisDomain.ActualizarAsync(codigo, pais);
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
                respuesta.Dato = await _paisDomain.EliminarAsync(codigo);
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

        public async Task<Respuesta<PaisDTO>> ObtenerPorCodigoAsync(string codigo)
        {
            var respuesta = new Respuesta<PaisDTO>();
            try
            {
                var pais = await _paisDomain.ObtenerPorCodigoAsync(codigo);
                respuesta.Dato = _mapper.Map<PaisDTO>(pais);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<PaisDTO>> ObtenerPorNombreAsync(string nombre)
        {
            var respuesta = new Respuesta<PaisDTO>();
            try
            {
                var pais = await _paisDomain.ObtenerPorNombreAsync(nombre);
                respuesta.Dato = _mapper.Map<PaisDTO>(pais);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PaisDTO>>> ObtenerAsync()
        {
            var respuesta = new Respuesta<IEnumerable<PaisDTO>>();
            try
            {
                var paises = await _paisDomain.ObtenerTodoAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<PaisDTO>>(paises);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PaisDTO>>> ObtenerContengaNombreAsync(string nombre)
        {
            var respuesta = new Respuesta<IEnumerable<PaisDTO>>();
            try
            {
                var paises = await _paisDomain.ObtenerContengaNombreAsync(nombre);
                respuesta.Dato = _mapper.Map<IEnumerable<PaisDTO>>(paises);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PaisDTO>>> ObtenerContengaCodigoAsync(string codigo)
        {
            var respuesta = new Respuesta<IEnumerable<PaisDTO>>();

            try
            {
                var paises = await _paisDomain.ObtenerContengaCodigoAsync(codigo);
                respuesta.Dato = _mapper.Map<IEnumerable<PaisDTO>>(paises);

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
