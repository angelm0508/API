using API.Application.DTO;
using API.Application.DTO.entrega;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class EntregaApplication : IEntregaApplication
    {
        private readonly IEntregaDomain _entregaDomain;
        private readonly IMapper _mapper;

        public EntregaApplication(IEntregaDomain entregaDomain, IMapper mapper)
        {
            _entregaDomain = entregaDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(EntregaCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var entrega = _mapper.Map<Entrega>(obj);
                respuesta.Dato = await _entregaDomain.InsertarAsync(entrega);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, EntregaActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var entrega = _mapper.Map<Entrega>(obj);
                respuesta.Dato = await _entregaDomain.ActualizarAsync(id, entrega);
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
                respuesta.Dato = await _entregaDomain.EliminarAsync(id);
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

        public async Task<Respuesta<EntregaDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<EntregaDTO>();
            try
            {
                var entrega = await _entregaDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<EntregaDTO>(entrega);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<EntregaDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<EntregaDTO>>();
            try
            {
                var queryable = await _entregaDomain.ObtenerTodoAsync();
                var entregas = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<EntregaDTO>>(entregas);
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
