using API.Application.DTO;
using API.Application.DTO.notaCredito;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class NotaCreditoApplication : INotaCreditoApplication
    {
        private readonly INotaCreditoDomain _domain;
        private readonly IMapper _mapper;

        public NotaCreditoApplication(INotaCreditoDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(NotaCreditoCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var nota = _mapper.Map<NotaCredito>(obj);
                var lineas = _mapper.Map<IEnumerable<NotaCreditoDetalle>>(obj.Lineas);
                respuesta.Dato = await _domain.InsertarAsync(nota, lineas);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, NotaCreditoActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var nota = _mapper.Map<NotaCredito>(obj);
                respuesta.Dato = await _domain.ActualizarAsync(id, nota);
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
                respuesta.Dato = await _domain.EliminarAsync(id);
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

        public async Task<Respuesta<NotaCreditoDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<NotaCreditoDTO>();
            try
            {
                var nota = await _domain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<NotaCreditoDTO>(nota);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<NotaCreditoDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<NotaCreditoDTO>>();
            try
            {
                var queryable = await _domain.ObtenerTodoAsync();
                var notas = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<NotaCreditoDTO>>(notas);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<NotaCreditoDetalleDTO>>> ObtenerLineasAsync(int id)
        {
            var respuesta = new Respuesta<IEnumerable<NotaCreditoDetalleDTO>>();
            try
            {
                var lineas = await _domain.ObtenerLineasAsync(id);
                respuesta.Dato = _mapper.Map<IEnumerable<NotaCreditoDetalleDTO>>(lineas);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<NotaCreditoFacturaBaseResumenDTO>>> ObtenerFacturasBaseAsync(string codigoSn)
        {
            var respuesta = new Respuesta<IEnumerable<NotaCreditoFacturaBaseResumenDTO>>();
            try
            {
                var facturas = await _domain.ObtenerFacturasBaseAsync(codigoSn);
                respuesta.Dato = facturas.Select(f => new NotaCreditoFacturaBaseResumenDTO
                {
                    Entry = f.Documento.Entry,
                    NumDoc = f.Documento.NumDoc,
                    FechaDoc = f.Documento.FechaDoc,
                    TotalDoc = f.Documento.TotalDoc,
                    MonedaDoc = f.Documento.MonedaDoc,
                    TotalAcreditado = f.TotalAcreditado,
                    Disponible = Disponible(f.Documento.TotalDoc, f.TotalAcreditado)
                }).ToList();
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<NotaCreditoFacturaBaseDTO>> ObtenerFacturaBaseAsync(int entry)
        {
            var respuesta = new Respuesta<NotaCreditoFacturaBaseDTO>();
            try
            {
                var baseDoc = await _domain.ObtenerFacturaBaseAsync(entry);
                respuesta.Dato = new NotaCreditoFacturaBaseDTO
                {
                    Entry = baseDoc.Documento.Entry,
                    NumDoc = baseDoc.Documento.NumDoc,
                    CodigoSn = baseDoc.Documento.CodigoSn,
                    NombreSn = baseDoc.Documento.NombreSn,
                    MonedaDoc = baseDoc.Documento.MonedaDoc,
                    Direccion = baseDoc.Documento.Direccion,
                    TotalDoc = baseDoc.Documento.TotalDoc,
                    TotalAcreditado = baseDoc.TotalAcreditado,
                    Disponible = Disponible(baseDoc.Documento.TotalDoc, baseDoc.TotalAcreditado),
                    // Las lineas de la factura se convierten en conceptos: sin articulo ni almacen.
                    Lineas = baseDoc.Lineas.Select(l => new NotaCreditoDetalleCrearDTO
                    {
                        Descripcion = string.IsNullOrWhiteSpace(l.Descripcion) ? (l.CodArticulo ?? string.Empty) : l.Descripcion,
                        Cantidad = l.Cantidad,
                        Precio = l.Precio,
                        PrecioBruto = l.PrecioBruto,
                        PrctjeDesc = l.PrctjeDesc,
                        CodigoImpuesto = l.CodigoImpuesto,
                        Impuesto = l.Impuesto,
                        TotalLinea = l.TotalLinea
                    }).ToList()
                };
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
        #endregion

        /// <summary>Lo que todavia se puede acreditar de una factura: max(0, total - acreditado), a 2 decimales.</summary>
        private static decimal Disponible(decimal? totalFactura, decimal acreditado) =>
            Math.Max(0m, Math.Round((totalFactura ?? 0m) - acreditado, 2, MidpointRounding.AwayFromZero));
    }
}
