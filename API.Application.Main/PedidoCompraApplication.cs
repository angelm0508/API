using API.Application.DTO;
using API.Application.DTO.pedidoCompra;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class PedidoCompraApplication : IPedidoCompraApplication
    {
        private readonly IPedidoCompraDomain _pedidoCompraDomain;
        private readonly IMapper _mapper;

        public PedidoCompraApplication(IPedidoCompraDomain pedidoCompraDomain, IMapper mapper)
        {
            _pedidoCompraDomain = pedidoCompraDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(PedidoCompraCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var pedidoCompra = _mapper.Map<PedidoCompra>(obj);
                respuesta.Dato = await _pedidoCompraDomain.InsertarAsync(pedidoCompra);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, PedidoCompraActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var pedidoCompra = _mapper.Map<PedidoCompra>(obj);
                respuesta.Dato = await _pedidoCompraDomain.ActualizarAsync(id, pedidoCompra);
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
                respuesta.Dato = await _pedidoCompraDomain.EliminarAsync(id);
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

        public async Task<Respuesta<PedidoCompraDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<PedidoCompraDTO>();
            try
            {
                var pedidoCompra = await _pedidoCompraDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<PedidoCompraDTO>(pedidoCompra);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PedidoCompraDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<PedidoCompraDTO>>();
            try
            {
                var queryable = await _pedidoCompraDomain.ObtenerTodoAsync();
                var pedidoCompras = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<PedidoCompraDTO>>(pedidoCompras);
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
