using API.Application.DTO;
using API.Application.DTO.facturaCompra;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class FacturaCompraApplication : IFacturaCompraApplication
    {
        private readonly IFacturaCompraDomain _facturaCompraDomain;
        private readonly IMapper _mapper;

        public FacturaCompraApplication(IFacturaCompraDomain facturaCompraDomain, IMapper mapper)
        {
            _facturaCompraDomain = facturaCompraDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(FacturaCompraCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var facturaCompra = _mapper.Map<FacturaCompra>(obj);
                respuesta.Dato = await _facturaCompraDomain.InsertarAsync(facturaCompra);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, FacturaCompraActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var facturaCompra = _mapper.Map<FacturaCompra>(obj);
                respuesta.Dato = await _facturaCompraDomain.ActualizarAsync(id, facturaCompra);
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
                respuesta.Dato = await _facturaCompraDomain.EliminarAsync(id);
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

        public async Task<Respuesta<FacturaCompraDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<FacturaCompraDTO>();
            try
            {
                var facturaCompra = await _facturaCompraDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<FacturaCompraDTO>(facturaCompra);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<FacturaCompraDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<FacturaCompraDTO>>();
            try
            {
                var queryable = await _facturaCompraDomain.ObtenerTodoAsync();
                var facturasCompra = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<FacturaCompraDTO>>(facturasCompra);
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
