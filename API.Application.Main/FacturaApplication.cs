using API.Application.DTO;
using API.Application.DTO.factura;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class FacturaApplication : IFacturaApplication
    {
        private readonly IFacturaDomain _facturaDomain;
        private readonly IMapper _mapper;

        public FacturaApplication(IFacturaDomain facturaDomain, IMapper mapper)
        {
            _facturaDomain = facturaDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(FacturaCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var factura = _mapper.Map<Factura>(obj);
                var lineas = _mapper.Map<IEnumerable<FacturaDetalle>>(obj.Lineas);
                respuesta.Dato = await _facturaDomain.InsertarAsync(factura, lineas);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, FacturaActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var factura = _mapper.Map<Factura>(obj);
                respuesta.Dato = await _facturaDomain.ActualizarAsync(id, factura);
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
                respuesta.Dato = await _facturaDomain.EliminarAsync(id);
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

        public async Task<Respuesta<FacturaDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<FacturaDTO>();
            try
            {
                var factura = await _facturaDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<FacturaDTO>(factura);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<FacturaDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<FacturaDTO>>();
            try
            {
                var queryable = await _facturaDomain.ObtenerTodoAsync();
                var facturas = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<FacturaDTO>>(facturas);
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
