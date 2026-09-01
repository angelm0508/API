using API.Application.DTO;
using API.Application.DTO.entradaMercancia;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class EntradaMercanciaApplication : IEntradaMercanciaApplication
    {
        private readonly IEntradaMercanciaDomain _entradaMercanciaDomain;
        private readonly IMapper _mapper;

        public EntradaMercanciaApplication(IEntradaMercanciaDomain entradaMercanciaDomain, IMapper mapper)
        {
            _entradaMercanciaDomain = entradaMercanciaDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(EntradaMercanciaCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var entradaMercancia = _mapper.Map<EntradaMercancia>(obj);
                var lineas = _mapper.Map<IEnumerable<EntradaMercanciaDetalle>>(obj.Lineas);
                respuesta.Dato = await _entradaMercanciaDomain.InsertarAsync(entradaMercancia, lineas);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, EntradaMercanciaActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var entradaMercancia = _mapper.Map<EntradaMercancia>(obj);
                respuesta.Dato = await _entradaMercanciaDomain.ActualizarAsync(id, entradaMercancia);
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
                respuesta.Dato = await _entradaMercanciaDomain.EliminarAsync(id);
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

        public async Task<Respuesta<EntradaMercanciaDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<EntradaMercanciaDTO>();
            try
            {
                var entradaMercancia = await _entradaMercanciaDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<EntradaMercanciaDTO>(entradaMercancia);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<EntradaMercanciaDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<EntradaMercanciaDTO>>();
            try
            {
                var queryable = await _entradaMercanciaDomain.ObtenerTodoAsync();
                var entradaMercancias = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<EntradaMercanciaDTO>>(entradaMercancias);
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
