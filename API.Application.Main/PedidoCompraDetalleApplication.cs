using API.Application.DTO;
using API.Application.DTO.pedidoCompra;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class PedidoCompraDetalleApplication : IPedidoCompraDetalleApplication
    {
        private readonly IPedidoCompraDetalleDomain _pedidoCompraDetalleDomain;
        private readonly IMapper _mapper;

        public PedidoCompraDetalleApplication(IPedidoCompraDetalleDomain pedidoCompraDetalleDomain, IMapper mapper)
        {
            _pedidoCompraDetalleDomain = pedidoCompraDetalleDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(PedidoCompraDetalleCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var entidad = _mapper.Map<PedidoCompraDetalle>(obj);
                respuesta.Dato = await _pedidoCompraDetalleDomain.InsertarAsync(entidad);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, PedidoCompraDetalleActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var entidad = _mapper.Map<PedidoCompraDetalle>(obj);
                respuesta.Dato = await _pedidoCompraDetalleDomain.ActualizarAsync(entry, noLinea, entidad);
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
                respuesta.Dato = await _pedidoCompraDetalleDomain.EliminarAsync(entry, noLinea);
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

        public async Task<Respuesta<PedidoCompraDetalleDTO>> ObtenerAsync(int entry, int noLinea)
        {
            var respuesta = new Respuesta<PedidoCompraDetalleDTO>();
            try
            {
                var entidad = await _pedidoCompraDetalleDomain.ObtenerAsync(entry, noLinea);
                respuesta.Dato = _mapper.Map<PedidoCompraDetalleDTO>(entidad);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PedidoCompraDetalleDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<PedidoCompraDetalleDTO>>();
            try
            {
                var queryable = await _pedidoCompraDetalleDomain.ObtenerTodoAsync();
                var lista = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<PedidoCompraDetalleDTO>>(lista);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PedidoCompraDetalleDTO>>> ObtenerPorPedidoCompraAsync(int entry)
        {
            var respuesta = new Respuesta<IEnumerable<PedidoCompraDetalleDTO>>();
            try
            {
                var lista = await _pedidoCompraDetalleDomain.ObtenerPorPedidoCompraAsync(entry);
                respuesta.Dato = _mapper.Map<IEnumerable<PedidoCompraDetalleDTO>>(lista);
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
