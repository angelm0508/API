using API.Application.DTO;
using API.Application.DTO.cotizacion;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class CotizacionDetalleApplication : ICotizacionDetalleApplication
    {
        private readonly ICotizacionDetalleDomain _cotizacionDetalleDomain;
        private readonly IMapper _mapper;

        public CotizacionDetalleApplication(ICotizacionDetalleDomain cotizacionDetalleDomain, IMapper mapper)
        {
            _cotizacionDetalleDomain = cotizacionDetalleDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(CotizacionDetalleCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var entidad = _mapper.Map<CotizacionDetalle>(obj);
                respuesta.Dato = await _cotizacionDetalleDomain.InsertarAsync(entidad);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, CotizacionDetalleActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var entidad = _mapper.Map<CotizacionDetalle>(obj);
                respuesta.Dato = await _cotizacionDetalleDomain.ActualizarAsync(entry, noLinea, entidad);
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
                respuesta.Dato = await _cotizacionDetalleDomain.EliminarAsync(entry, noLinea);
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

        public async Task<Respuesta<CotizacionDetalleDTO>> ObtenerAsync(int entry, int noLinea)
        {
            var respuesta = new Respuesta<CotizacionDetalleDTO>();
            try
            {
                var entidad = await _cotizacionDetalleDomain.ObtenerAsync(entry, noLinea);
                respuesta.Dato = _mapper.Map<CotizacionDetalleDTO>(entidad);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<CotizacionDetalleDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<CotizacionDetalleDTO>>();
            try
            {
                var queryable = await _cotizacionDetalleDomain.ObtenerTodoAsync();
                var lista = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<CotizacionDetalleDTO>>(lista);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<CotizacionDetalleDTO>>> ObtenerPorCotizacionAsync(int entry)
        {
            var respuesta = new Respuesta<IEnumerable<CotizacionDetalleDTO>>();
            try
            {
                var lista = await _cotizacionDetalleDomain.ObtenerPorCotizacionAsync(entry);
                respuesta.Dato = _mapper.Map<IEnumerable<CotizacionDetalleDTO>>(lista);
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
