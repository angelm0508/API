using API.Application.DTO;
using API.Application.DTO.salidaMercancia;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class SalidaMercanciaApplication : ISalidaMercanciaApplication
    {
        private readonly ISalidaMercanciaDomain _salidaMercanciaDomain;
        private readonly IMapper _mapper;

        public SalidaMercanciaApplication(ISalidaMercanciaDomain salidaMercanciaDomain, IMapper mapper)
        {
            _salidaMercanciaDomain = salidaMercanciaDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(SalidaMercanciaCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var salidaMercancia = _mapper.Map<SalidaMercancia>(obj);
                var lineas = _mapper.Map<IEnumerable<SalidaMercanciaDetalle>>(obj.Lineas);
                respuesta.Dato = await _salidaMercanciaDomain.InsertarAsync(salidaMercancia, lineas);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, SalidaMercanciaActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var salidaMercancia = _mapper.Map<SalidaMercancia>(obj);
                respuesta.Dato = await _salidaMercanciaDomain.ActualizarAsync(id, salidaMercancia);
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
                respuesta.Dato = await _salidaMercanciaDomain.EliminarAsync(id);
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

        public async Task<Respuesta<SalidaMercanciaDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<SalidaMercanciaDTO>();
            try
            {
                var salidaMercancia = await _salidaMercanciaDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<SalidaMercanciaDTO>(salidaMercancia);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<SalidaMercanciaDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<SalidaMercanciaDTO>>();
            try
            {
                var queryable = await _salidaMercanciaDomain.ObtenerTodoAsync();
                var salidaMercancias = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<SalidaMercanciaDTO>>(salidaMercancias);
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
