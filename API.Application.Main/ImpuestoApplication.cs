using API.Application.DTO;
using API.Application.DTO.impuesto;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;

namespace API.Application.Main
{
    public class ImpuestoApplication : IImpuestoApplication
    {
        private readonly IImpuestoDomain _impuestoDomain;
        private readonly IMapper _mapper;

        public ImpuestoApplication(IImpuestoDomain impuestoDomain, IMapper mapper)
        {
            _impuestoDomain = impuestoDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<bool>> InsertarAsync(ImpuestoCrearDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var impuesto = _mapper.Map<Impuesto>(obj);
                respuesta.Dato = await _impuestoDomain.InsertarAsync(impuesto);
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

        public async Task<Respuesta<bool>> ActualizarAsync(string codigo, ImpuestoActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var impuesto = _mapper.Map<Impuesto>(obj);
                respuesta.Dato = await _impuestoDomain.ActualizarAsync(codigo, impuesto);
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
                respuesta.Dato = await _impuestoDomain.EliminarAsync(codigo);
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

        public async Task<Respuesta<ImpuestoDTO>> ObtenerPorCodigoAsync(string codigo)
        {
            var respuesta = new Respuesta<ImpuestoDTO>();
            try
            {
                var impuesto = await _impuestoDomain.ObtenerPorCodigoAsync(codigo);
                respuesta.Dato = _mapper.Map<ImpuestoDTO>(impuesto);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<ImpuestoDTO>>> ObtenerAsync()
        {
            var respuesta = new Respuesta<IEnumerable<ImpuestoDTO>>();
            try
            {
                var impuestos = await _impuestoDomain.ObtenerTodoAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<ImpuestoDTO>>(impuestos);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<ImpuestoDTO>>> ObtenerContengaNombreAsync(string nombre)
        {
            var respuesta = new Respuesta<IEnumerable<ImpuestoDTO>>();
            try
            {
                var impuestos = await _impuestoDomain.ObtenerContengaNombreAsync(nombre);
                respuesta.Dato = _mapper.Map<IEnumerable<ImpuestoDTO>>(impuestos);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
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
