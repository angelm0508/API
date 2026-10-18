using API.Application.DTO;
using API.Application.DTO.notaCreditoCompra;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class NotaCreditoCompraApplication : INotaCreditoCompraApplication
    {
        private readonly INotaCreditoCompraDomain _domain;
        private readonly IMapper _mapper;

        public NotaCreditoCompraApplication(INotaCreditoCompraDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(NotaCreditoCompraCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var nota = _mapper.Map<NotaCreditoCompra>(obj);
                var lineas = _mapper.Map<IEnumerable<NotaCreditoCompraDetalle>>(obj.Lineas);
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

        public async Task<Respuesta<bool>> ActualizarAsync(int id, NotaCreditoCompraActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var nota = _mapper.Map<NotaCreditoCompra>(obj);
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

        public async Task<Respuesta<NotaCreditoCompraDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<NotaCreditoCompraDTO>();
            try
            {
                var nota = await _domain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<NotaCreditoCompraDTO>(nota);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<NotaCreditoCompraDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<NotaCreditoCompraDTO>>();
            try
            {
                var queryable = await _domain.ObtenerTodoAsync();
                var notas = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<NotaCreditoCompraDTO>>(notas);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<NotaCreditoCompraDetalleDTO>>> ObtenerLineasAsync(int id)
        {
            var respuesta = new Respuesta<IEnumerable<NotaCreditoCompraDetalleDTO>>();
            try
            {
                var lineas = await _domain.ObtenerLineasAsync(id);
                respuesta.Dato = _mapper.Map<IEnumerable<NotaCreditoCompraDetalleDTO>>(lineas);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<NotaCreditoCompraFacturaBaseResumenDTO>>> ObtenerFacturasCompraBaseAsync(string codigoSn)
        {
            var respuesta = new Respuesta<IEnumerable<NotaCreditoCompraFacturaBaseResumenDTO>>();
            try
            {
                var facturas = await _domain.ObtenerFacturasCompraBaseAsync(codigoSn);
                respuesta.Dato = facturas.Select(f => new NotaCreditoCompraFacturaBaseResumenDTO
                {
                    Entry = f.Documento.Entry,
                    NumDoc = f.Documento.NumDoc,
                    FechaDoc = f.Documento.FechaDoc,
                    TotalDoc = f.Documento.TotalDoc,
                    MonedaDoc = f.Documento.MonedaDoc,
                    DocSubType = f.Documento.DocSubType,
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

        public async Task<Respuesta<NotaCreditoCompraFacturaBaseDTO>> ObtenerFacturaCompraBaseAsync(int entry)
        {
            var respuesta = new Respuesta<NotaCreditoCompraFacturaBaseDTO>();
            try
            {
                var baseDoc = await _domain.ObtenerFacturaCompraBaseAsync(entry);
                respuesta.Dato = new NotaCreditoCompraFacturaBaseDTO
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
                    Lineas = baseDoc.Lineas.Select(l => new NotaCreditoCompraDetalleCrearDTO
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
