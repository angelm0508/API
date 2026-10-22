using API.Application.DTO;
using API.Application.DTO.ordenServicio;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class OrdenServicioApplication : IOrdenServicioApplication
    {
        private readonly IOrdenServicioDomain _domain;
        private readonly IMapper _mapper;

        public OrdenServicioApplication(IOrdenServicioDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(OrdenServicioCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var orden = _mapper.Map<OrdenServicio>(obj);
                var lineas = _mapper.Map<IEnumerable<OrdenServicioDetalle>>(obj.Lineas);
                respuesta.Dato = await _domain.InsertarAsync(orden, lineas);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, OrdenServicioActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.ActualizarAsync(id, _mapper.Map<OrdenServicio>(obj));
                respuesta.Resultado = respuesta.Dato;
                respuesta.Mensaje = respuesta.Dato ? "Registro actualizado correctamente." : "La orden de servicio no se encontró.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> CambiarEstadoAsync(int id, string estado)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.CambiarEstadoAsync(id, estado);
                respuesta.Resultado = respuesta.Dato;
                respuesta.Mensaje = respuesta.Dato ? "Estado actualizado correctamente." : "La orden de servicio no se encontró.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> CancelarAsync(int id)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.CancelarAsync(id);
                respuesta.Resultado = respuesta.Dato;
                respuesta.Mensaje = respuesta.Dato
                    ? "Orden cancelada. El inventario de los insumos fue devuelto."
                    : "La orden de servicio no se encontró.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<OrdenServicioDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<OrdenServicioDTO>();
            try
            {
                respuesta.Dato = _mapper.Map<OrdenServicioDTO>(await _domain.ObtenerAsync(id));
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<OrdenServicioDTO>>> ObtenerTodoAsync(string? estado)
        {
            var respuesta = new Respuesta<IEnumerable<OrdenServicioDTO>>();
            try
            {
                var consulta = await _domain.ObtenerTodoAsync();
                if (!string.IsNullOrWhiteSpace(estado))
                {
                    var filtro = estado.Trim().ToUpperInvariant();
                    consulta = consulta.Where(x => x.EstadoServicio == filtro);
                }
                var ordenes = await consulta.OrderByDescending(x => x.Entry).ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<OrdenServicioDTO>>(ordenes);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<OrdenServicioDetalleDTO>>> ObtenerLineasAsync(int id)
        {
            var respuesta = new Respuesta<IEnumerable<OrdenServicioDetalleDTO>>();
            try
            {
                respuesta.Dato = _mapper.Map<IEnumerable<OrdenServicioDetalleDTO>>(await _domain.ObtenerLineasAsync(id));
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
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
