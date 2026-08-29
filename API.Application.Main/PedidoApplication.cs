using API.Application.DTO;
using API.Application.DTO.pedido;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class PedidoApplication : IPedidoApplication
    {
        private readonly IPedidoDomain _pedidoDomain;
        private readonly IMapper _mapper;

        public PedidoApplication(IPedidoDomain pedidoDomain, IMapper mapper)
        {
            _pedidoDomain = pedidoDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(PedidoCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var pedido = _mapper.Map<Pedido>(obj);
                respuesta.Dato = await _pedidoDomain.InsertarAsync(pedido);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, PedidoActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var pedido = _mapper.Map<Pedido>(obj);
                respuesta.Dato = await _pedidoDomain.ActualizarAsync(id, pedido);
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
                respuesta.Dato = await _pedidoDomain.EliminarAsync(id);
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

        public async Task<Respuesta<PedidoDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<PedidoDTO>();
            try
            {
                var pedido = await _pedidoDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<PedidoDTO>(pedido);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PedidoDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<PedidoDTO>>();
            try
            {
                var queryable = await _pedidoDomain.ObtenerTodoAsync();
                var pedidos = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<PedidoDTO>>(pedidos);
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
