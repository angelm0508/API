using API.Application.DTO;
using API.Application.DTO.ordenServicio;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;

namespace API.Application.Main
{
    public class OrdenServicioDetalleApplication : IOrdenServicioDetalleApplication
    {
        private const string LineaNoEncontrada = "La línea no se encontró.";

        private readonly IOrdenServicioDetalleDomain _domain;
        private readonly IMapper _mapper;

        public OrdenServicioDetalleApplication(IOrdenServicioDetalleDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(OrdenServicioDetalleCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                respuesta.Dato = await _domain.InsertarAsync(_mapper.Map<OrdenServicioDetalle>(obj));
                respuesta.Resultado = true;
                respuesta.Mensaje = "Línea agregada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, OrdenServicioDetalleActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.ActualizarAsync(entry, noLinea, _mapper.Map<OrdenServicioDetalle>(obj));
                respuesta.Resultado = respuesta.Dato;
                respuesta.Mensaje = respuesta.Dato ? "Línea actualizada correctamente." : LineaNoEncontrada;
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
                respuesta.Dato = await _domain.EliminarAsync(entry, noLinea);
                respuesta.Resultado = respuesta.Dato;
                respuesta.Mensaje = respuesta.Dato ? "Línea eliminada correctamente." : LineaNoEncontrada;
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
