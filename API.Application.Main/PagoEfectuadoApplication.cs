using API.Application.DTO;
using API.Application.DTO.pagoEfectuado;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class PagoEfectuadoApplication : IPagoEfectuadoApplication
    {
        private readonly IPagoEfectuadoDomain _domain;
        private readonly IMapper _mapper;

        public PagoEfectuadoApplication(IPagoEfectuadoDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(PagoEfectuadoCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var pago = _mapper.Map<PagoEfectuado>(obj);
                var facturas = _mapper.Map<IEnumerable<PagoEfectuadoFactura>>(obj.Facturas ?? new());
                var medios = _mapper.Map<IEnumerable<PagoEfectuadoMedio>>(obj.Medios ?? new());
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
                respuesta.Mensaje = respuesta.Dato ? "Pago cancelado correctamente." : "El pago efectuado no se encontró.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<PagoEfectuadoDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<PagoEfectuadoDTO>();
            try
            {
                respuesta.Dato = _mapper.Map<PagoEfectuadoDTO>(await _domain.ObtenerAsync(id));
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PagoEfectuadoDTO>>> ObtenerTodosAsync()
        {
            var respuesta = new Respuesta<IEnumerable<PagoEfectuadoDTO>>();
            try
            {
                var pagos = await (await _domain.ObtenerTodosAsync()).OrderByDescending(p => p.Entry).ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<PagoEfectuadoDTO>>(pagos);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PagoEfectuadoFacturaDTO>>> ObtenerFacturasAsync(int id)
        {
            var respuesta = new Respuesta<IEnumerable<PagoEfectuadoFacturaDTO>>();
            try
            {
                respuesta.Dato = _mapper.Map<IEnumerable<PagoEfectuadoFacturaDTO>>(await _domain.ObtenerFacturasAsync(id));
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PagoEfectuadoMedioDTO>>> ObtenerMediosAsync(int id)
        {
            var respuesta = new Respuesta<IEnumerable<PagoEfectuadoMedioDTO>>();
            try
            {
                respuesta.Dato = _mapper.Map<IEnumerable<PagoEfectuadoMedioDTO>>(await _domain.ObtenerMediosAsync(id));
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<DocumentoPorPagarDTO>>> ObtenerAbiertosAsync(string codigoSn)
        {
            var respuesta = new Respuesta<IEnumerable<DocumentoPorPagarDTO>>();
            try
            {
                var documentos = await _domain.ObtenerAbiertosAsync(codigoSn);
                respuesta.Dato = documentos.Select(d => new DocumentoPorPagarDTO
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
