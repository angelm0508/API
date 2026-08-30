using API.Application.DTO;
using API.Application.DTO.facturaCompra;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class FacturaCompraDetalleApplication : IFacturaCompraDetalleApplication
    {
        private readonly IFacturaCompraDetalleDomain _facturaCompraDetalleDomain;
        private readonly IMapper _mapper;

        public FacturaCompraDetalleApplication(IFacturaCompraDetalleDomain facturaCompraDetalleDomain, IMapper mapper)
        {
            _facturaCompraDetalleDomain = facturaCompraDetalleDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(FacturaCompraDetalleCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var entidad = _mapper.Map<FacturaCompraDetalle>(obj);
                respuesta.Dato = await _facturaCompraDetalleDomain.InsertarAsync(entidad);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, FacturaCompraDetalleActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var entidad = _mapper.Map<FacturaCompraDetalle>(obj);
                respuesta.Dato = await _facturaCompraDetalleDomain.ActualizarAsync(entry, noLinea, entidad);
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
                respuesta.Dato = await _facturaCompraDetalleDomain.EliminarAsync(entry, noLinea);
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

        public async Task<Respuesta<FacturaCompraDetalleDTO>> ObtenerAsync(int entry, int noLinea)
        {
            var respuesta = new Respuesta<FacturaCompraDetalleDTO>();
            try
            {
                var entidad = await _facturaCompraDetalleDomain.ObtenerAsync(entry, noLinea);
                respuesta.Dato = _mapper.Map<FacturaCompraDetalleDTO>(entidad);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<FacturaCompraDetalleDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<FacturaCompraDetalleDTO>>();
            try
            {
                var queryable = await _facturaCompraDetalleDomain.ObtenerTodoAsync();
                var lista = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<FacturaCompraDetalleDTO>>(lista);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<FacturaCompraDetalleDTO>>> ObtenerPorFacturaCompraAsync(int entry)
        {
            var respuesta = new Respuesta<IEnumerable<FacturaCompraDetalleDTO>>();
            try
            {
                var lista = await _facturaCompraDetalleDomain.ObtenerPorFacturaCompraAsync(entry);
                respuesta.Dato = _mapper.Map<IEnumerable<FacturaCompraDetalleDTO>>(lista);
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
