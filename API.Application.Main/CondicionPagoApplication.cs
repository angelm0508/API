using API.Application.DTO;
using API.Application.DTO.condicionPago;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class CondicionPagoApplication : ICondicionPagoApplication
    {
        private readonly ICondicionPagoDomain _domain;
        private readonly IMapper _mapper;

        public CondicionPagoApplication(ICondicionPagoDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<bool>> InsertarAsync(CondicionPagoCrearDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.InsertarAsync(_mapper.Map<CondicionPago>(obj));
                respuesta.Resultado = respuesta.Dato;
                if (respuesta.Dato)
                    respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(string codigo, CondicionPagoActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.ActualizarAsync(codigo, _mapper.Map<CondicionPago>(obj));
                respuesta.Resultado = respuesta.Dato;
                if (respuesta.Dato)
                    respuesta.Mensaje = "Registro actualizado correctamente.";
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
                respuesta.Dato = await _domain.EliminarAsync(codigo);
                respuesta.Resultado = respuesta.Dato;
                if (respuesta.Dato)
                    respuesta.Mensaje = "Registro desactivado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<CondicionPagoDTO>> ObtenerPorCodigoAsync(string codigo)
        {
            var respuesta = new Respuesta<CondicionPagoDTO>();
            try
            {
                var condicion = await _domain.ObtenerPorCodigoAsync(codigo);
                respuesta.Dato = _mapper.Map<CondicionPagoDTO>(condicion);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<CondicionPagoDTO>>> ObtenerAsync()
        {
            var respuesta = new Respuesta<IEnumerable<CondicionPagoDTO>>();
            try
            {
                var condiciones = await (await _domain.ObtenerTodoAsync()).OrderBy(x => x.Dias).ThenBy(x => x.Nombre).ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<CondicionPagoDTO>>(condiciones);
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
