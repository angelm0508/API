using API.Application.DTO;
using API.Application.DTO.formaPago;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class FormaPagoApplication : IFormaPagoApplication
    {
        private readonly IFormaPagoDomain _domain;
        private readonly IMapper _mapper;

        public FormaPagoApplication(IFormaPagoDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<bool>> InsertarAsync(FormaPagoCrearDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.InsertarAsync(_mapper.Map<FormaPago>(obj));
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

        public async Task<Respuesta<bool>> ActualizarAsync(string codigo, FormaPagoActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.ActualizarAsync(codigo, _mapper.Map<FormaPago>(obj));
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

        public async Task<Respuesta<FormaPagoDTO>> ObtenerPorCodigoAsync(string codigo)
        {
            var respuesta = new Respuesta<FormaPagoDTO>();
            try
            {
                var forma = await _domain.ObtenerPorCodigoAsync(codigo);
                respuesta.Dato = _mapper.Map<FormaPagoDTO>(forma);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<FormaPagoDTO>>> ObtenerAsync()
        {
            var respuesta = new Respuesta<IEnumerable<FormaPagoDTO>>();
            try
            {
                var formas = await (await _domain.ObtenerTodoAsync()).OrderBy(x => x.Nombre).ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<FormaPagoDTO>>(formas);
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
