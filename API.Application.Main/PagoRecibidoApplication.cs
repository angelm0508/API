using API.Application.DTO;
using API.Application.DTO.pagoRecibido;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class PagoRecibidoApplication : IPagoRecibidoApplication
    {
        private readonly IPagoRecibidoDomain _domain;
        private readonly IMapper _mapper;

        public PagoRecibidoApplication(IPagoRecibidoDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(PagoRecibidoCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var pago = _mapper.Map<PagoRecibido>(obj);
                var facturas = _mapper.Map<IEnumerable<PagoRecibidoFactura>>(obj.Facturas ?? new());
                var medios = _mapper.Map<IEnumerable<PagoRecibidoMedio>>(obj.Medios ?? new());
                respuesta.Dato = await _domain.InsertarAsync(pago, facturas, medios);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Pago registrado correctamente.";
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
                respuesta.Mensaje = respuesta.Dato ? "Pago cancelado correctamente." : "El pago recibido no se encontró.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<PagoRecibidoDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<PagoRecibidoDTO>();
            try
            {
                respuesta.Dato = _mapper.Map<PagoRecibidoDTO>(await _domain.ObtenerAsync(id));
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PagoRecibidoDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<PagoRecibidoDTO>>();
            try
            {
                var pagos = await (await _domain.ObtenerTodoAsync()).OrderByDescending(p => p.Entry).ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<PagoRecibidoDTO>>(pagos);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PagoRecibidoFacturaDTO>>> ObtenerFacturasAsync(int id)
        {
            var respuesta = new Respuesta<IEnumerable<PagoRecibidoFacturaDTO>>();
            try
            {
                respuesta.Dato = _mapper.Map<IEnumerable<PagoRecibidoFacturaDTO>>(await _domain.ObtenerFacturasAsync(id));
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PagoRecibidoMedioDTO>>> ObtenerMediosAsync(int id)
        {
            var respuesta = new Respuesta<IEnumerable<PagoRecibidoMedioDTO>>();
            try
            {
                respuesta.Dato = _mapper.Map<IEnumerable<PagoRecibidoMedioDTO>>(await _domain.ObtenerMediosAsync(id));
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<DocumentoPorCobrarDTO>>> ObtenerDocumentosPorCobrarAsync(string codigoSn)
        {
            var respuesta = new Respuesta<IEnumerable<DocumentoPorCobrarDTO>>();
            try
            {
                var documentos = await _domain.ObtenerDocumentosPorCobrarAsync(codigoSn);
                respuesta.Dato = documentos.Select(d => new DocumentoPorCobrarDTO
                {
                    Entry = d.Documento.Entry,
                    NumDoc = d.Documento.NumDoc,
                    DocSubType = d.Documento.DocSubType,
                    FechaDoc = d.Documento.FechaDoc,
                    Total = d.Saldo.Total,
                    Acreditado = d.Saldo.Acreditado,
                    Pagado = d.Saldo.Pagado,
                    Saldo = d.Saldo.Saldo,
                    Estado = d.Saldo.Estado
                }).ToList();
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
