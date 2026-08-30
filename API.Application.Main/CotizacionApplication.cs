using API.Application.DTO;
using API.Application.DTO.cotizacion;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class CotizacionApplication : ICotizacionApplication
    {
        private readonly ICotizacionDomain _cotizacionDomain;
        private readonly IMapper _mapper;

        public CotizacionApplication(ICotizacionDomain cotizacionDomain, IMapper mapper)
        {
            _cotizacionDomain = cotizacionDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(CotizacionCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var cotizacion = _mapper.Map<Cotizacion>(obj);
                respuesta.Dato = await _cotizacionDomain.InsertarAsync(cotizacion);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, CotizacionActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var cotizacion = _mapper.Map<Cotizacion>(obj);
                respuesta.Dato = await _cotizacionDomain.ActualizarAsync(id, cotizacion);
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

        public async Task<Respuesta<bool>> EliminarAsync(int id)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _cotizacionDomain.EliminarAsync(id);
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

        public async Task<Respuesta<CotizacionDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<CotizacionDTO>();
            try
            {
                var cotizacion = await _cotizacionDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<CotizacionDTO>(cotizacion);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<CotizacionDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<CotizacionDTO>>();
            try
            {
                var queryable = await _cotizacionDomain.ObtenerTodoAsync();
                var cotizaciones = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<CotizacionDTO>>(cotizaciones);
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
