using API.Application.DTO;
using API.Application.DTO.entregaCompra;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class EntregaCompraApplication : IEntregaCompraApplication
    {
        private readonly IEntregaCompraDomain _entregaCompraDomain;
        private readonly IMapper _mapper;

        public EntregaCompraApplication(IEntregaCompraDomain entregaCompraDomain, IMapper mapper)
        {
            _entregaCompraDomain = entregaCompraDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(EntregaCompraCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var entregaCompra = _mapper.Map<EntregaCompra>(obj);
                var lineas = _mapper.Map<IEnumerable<EntregaCompraDetalle>>(obj.Lineas);
                respuesta.Dato = await _entregaCompraDomain.InsertarAsync(entregaCompra, lineas);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, EntregaCompraActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var entregaCompra = _mapper.Map<EntregaCompra>(obj);
                respuesta.Dato = await _entregaCompraDomain.ActualizarAsync(id, entregaCompra);
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
                respuesta.Dato = await _entregaCompraDomain.EliminarAsync(id);
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

        public async Task<Respuesta<EntregaCompraDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<EntregaCompraDTO>();
            try
            {
                var entregaCompra = await _entregaCompraDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<EntregaCompraDTO>(entregaCompra);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<EntregaCompraDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<EntregaCompraDTO>>();
            try
            {
                var queryable = await _entregaCompraDomain.ObtenerTodoAsync();
                var entregaCompras = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<EntregaCompraDTO>>(entregaCompras);
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
