using API.Application.DTO;
using API.Application.DTO.entradaMercancia;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class EntradaMercanciaDetalleApplication : IEntradaMercanciaDetalleApplication
    {
        private readonly IEntradaMercanciaDetalleDomain _entradaMercanciaDetalleDomain;
        private readonly IMapper _mapper;

        public EntradaMercanciaDetalleApplication(IEntradaMercanciaDetalleDomain entradaMercanciaDetalleDomain, IMapper mapper)
        {
            _entradaMercanciaDetalleDomain = entradaMercanciaDetalleDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(EntradaMercanciaDetalleCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var entidad = _mapper.Map<EntradaMercanciaDetalle>(obj);
                respuesta.Dato = await _entradaMercanciaDetalleDomain.InsertarAsync(entidad);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, EntradaMercanciaDetalleActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var entidad = _mapper.Map<EntradaMercanciaDetalle>(obj);
                respuesta.Dato = await _entradaMercanciaDetalleDomain.ActualizarAsync(entry, noLinea, entidad);
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
                respuesta.Dato = await _entradaMercanciaDetalleDomain.EliminarAsync(entry, noLinea);
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

        public async Task<Respuesta<EntradaMercanciaDetalleDTO>> ObtenerAsync(int entry, int noLinea)
        {
            var respuesta = new Respuesta<EntradaMercanciaDetalleDTO>();
            try
            {
                var entidad = await _entradaMercanciaDetalleDomain.ObtenerAsync(entry, noLinea);
                respuesta.Dato = _mapper.Map<EntradaMercanciaDetalleDTO>(entidad);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<EntradaMercanciaDetalleDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<EntradaMercanciaDetalleDTO>>();
            try
            {
                var queryable = await _entradaMercanciaDetalleDomain.ObtenerTodoAsync();
                var lista = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<EntradaMercanciaDetalleDTO>>(lista);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<EntradaMercanciaDetalleDTO>>> ObtenerPorEntradaMercanciaAsync(int entry)
        {
            var respuesta = new Respuesta<IEnumerable<EntradaMercanciaDetalleDTO>>();
            try
            {
                var lista = await _entradaMercanciaDetalleDomain.ObtenerPorEntradaMercanciaAsync(entry);
                respuesta.Dato = _mapper.Map<IEnumerable<EntradaMercanciaDetalleDTO>>(lista);
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
