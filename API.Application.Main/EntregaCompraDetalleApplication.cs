using API.Application.DTO;
using API.Application.DTO.entregaCompra;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class EntregaCompraDetalleApplication : IEntregaCompraDetalleApplication
    {
        private readonly IEntregaCompraDetalleDomain _entregaCompraDetalleDomain;
        private readonly IMapper _mapper;

        public EntregaCompraDetalleApplication(IEntregaCompraDetalleDomain entregaCompraDetalleDomain, IMapper mapper)
        {
            _entregaCompraDetalleDomain = entregaCompraDetalleDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(EntregaCompraDetalleCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var entidad = _mapper.Map<EntregaCompraDetalle>(obj);
                respuesta.Dato = await _entregaCompraDetalleDomain.InsertarAsync(entidad);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, EntregaCompraDetalleActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var entidad = _mapper.Map<EntregaCompraDetalle>(obj);
                respuesta.Dato = await _entregaCompraDetalleDomain.ActualizarAsync(entry, noLinea, entidad);
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
                respuesta.Dato = await _entregaCompraDetalleDomain.EliminarAsync(entry, noLinea);
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

        public async Task<Respuesta<EntregaCompraDetalleDTO>> ObtenerAsync(int entry, int noLinea)
        {
            var respuesta = new Respuesta<EntregaCompraDetalleDTO>();
            try
            {
                var entidad = await _entregaCompraDetalleDomain.ObtenerAsync(entry, noLinea);
                respuesta.Dato = _mapper.Map<EntregaCompraDetalleDTO>(entidad);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<EntregaCompraDetalleDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<EntregaCompraDetalleDTO>>();
            try
            {
                var queryable = await _entregaCompraDetalleDomain.ObtenerTodoAsync();
                var lista = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<EntregaCompraDetalleDTO>>(lista);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<EntregaCompraDetalleDTO>>> ObtenerPorEntregaCompraAsync(int entry)
        {
            var respuesta = new Respuesta<IEnumerable<EntregaCompraDetalleDTO>>();
            try
            {
                var lista = await _entregaCompraDetalleDomain.ObtenerPorEntregaCompraAsync(entry);
                respuesta.Dato = _mapper.Map<IEnumerable<EntregaCompraDetalleDTO>>(lista);
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
