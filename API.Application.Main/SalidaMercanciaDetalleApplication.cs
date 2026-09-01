using API.Application.DTO;
using API.Application.DTO.salidaMercancia;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class SalidaMercanciaDetalleApplication : ISalidaMercanciaDetalleApplication
    {
        private readonly ISalidaMercanciaDetalleDomain _salidaMercanciaDetalleDomain;
        private readonly IMapper _mapper;

        public SalidaMercanciaDetalleApplication(ISalidaMercanciaDetalleDomain salidaMercanciaDetalleDomain, IMapper mapper)
        {
            _salidaMercanciaDetalleDomain = salidaMercanciaDetalleDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(SalidaMercanciaDetalleCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var entidad = _mapper.Map<SalidaMercanciaDetalle>(obj);
                respuesta.Dato = await _salidaMercanciaDetalleDomain.InsertarAsync(entidad);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, SalidaMercanciaDetalleActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var entidad = _mapper.Map<SalidaMercanciaDetalle>(obj);
                respuesta.Dato = await _salidaMercanciaDetalleDomain.ActualizarAsync(entry, noLinea, entidad);
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

        public async Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _salidaMercanciaDetalleDomain.EliminarAsync(entry, noLinea);
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

        public async Task<Respuesta<SalidaMercanciaDetalleDTO>> ObtenerAsync(int entry, int noLinea)
        {
            var respuesta = new Respuesta<SalidaMercanciaDetalleDTO>();
            try
            {
                var entidad = await _salidaMercanciaDetalleDomain.ObtenerAsync(entry, noLinea);
                respuesta.Dato = _mapper.Map<SalidaMercanciaDetalleDTO>(entidad);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<SalidaMercanciaDetalleDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<SalidaMercanciaDetalleDTO>>();
            try
            {
                var queryable = await _salidaMercanciaDetalleDomain.ObtenerTodoAsync();
                var lista = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<SalidaMercanciaDetalleDTO>>(lista);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<SalidaMercanciaDetalleDTO>>> ObtenerPorSalidaMercanciaAsync(int entry)
        {
            var respuesta = new Respuesta<IEnumerable<SalidaMercanciaDetalleDTO>>();
            try
            {
                var lista = await _salidaMercanciaDetalleDomain.ObtenerPorSalidaMercanciaAsync(entry);
                respuesta.Dato = _mapper.Map<IEnumerable<SalidaMercanciaDetalleDTO>>(lista);
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
