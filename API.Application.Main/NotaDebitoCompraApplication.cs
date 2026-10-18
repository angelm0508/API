using API.Application.DTO;
using API.Application.DTO.notaDebitoCompra;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class NotaDebitoCompraApplication : INotaDebitoCompraApplication
    {
        private readonly INotaDebitoCompraDomain _domain;
        private readonly IMapper _mapper;

        public NotaDebitoCompraApplication(INotaDebitoCompraDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(NotaDebitoCompraCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var nota = _mapper.Map<FacturaCompra>(obj);
                var lineas = _mapper.Map<IEnumerable<FacturaCompraDetalle>>(obj.Lineas);
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

        public async Task<Respuesta<bool>> ActualizarAsync(int id, NotaDebitoCompraActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var nota = _mapper.Map<FacturaCompra>(obj);
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

        public async Task<Respuesta<NotaDebitoCompraDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<NotaDebitoCompraDTO>();
            try
            {
                var nota = await _domain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<NotaDebitoCompraDTO>(nota);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<NotaDebitoCompraDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<NotaDebitoCompraDTO>>();
            try
            {
                var queryable = await _domain.ObtenerTodoAsync();
                var notas = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<NotaDebitoCompraDTO>>(notas);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<NotaDebitoCompraDetalleDTO>>> ObtenerLineasAsync(int id)
        {
            var respuesta = new Respuesta<IEnumerable<NotaDebitoCompraDetalleDTO>>();
            try
            {
                var lineas = await _domain.ObtenerLineasAsync(id);
                respuesta.Dato = _mapper.Map<IEnumerable<NotaDebitoCompraDetalleDTO>>(lineas);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<NotaDebitoCompraFacturaBaseResumenDTO>>> ObtenerFacturasCompraBaseAsync(string codigoSn)
        {
            var respuesta = new Respuesta<IEnumerable<NotaDebitoCompraFacturaBaseResumenDTO>>();
            try
            {
                var facturas = await _domain.ObtenerFacturasCompraBaseAsync(codigoSn);
                respuesta.Dato = facturas.Select(f => new NotaDebitoCompraFacturaBaseResumenDTO
                {
                    Entry = f.Entry,
                    NumDoc = f.NumDoc,
                    FechaDoc = f.FechaDoc,
                    TotalDoc = f.TotalDoc,
                    MonedaDoc = f.MonedaDoc
                }).ToList();
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<NotaDebitoCompraFacturaBaseDTO>> ObtenerFacturaCompraBaseAsync(int entry)
        {
            var respuesta = new Respuesta<NotaDebitoCompraFacturaBaseDTO>();
            try
            {
                var baseDoc = await _domain.ObtenerFacturaCompraBaseAsync(entry);
                respuesta.Dato = new NotaDebitoCompraFacturaBaseDTO
                {
                    Entry = baseDoc.Documento.Entry,
                    NumDoc = baseDoc.Documento.NumDoc,
                    CodigoSn = baseDoc.Documento.CodigoSn,
                    NombreSn = baseDoc.Documento.NombreSn,
                    MonedaDoc = baseDoc.Documento.MonedaDoc,
                    Direccion = baseDoc.Documento.Direccion,
                    // Las lineas de la factura se devuelven con su articulo y almacen: la nota por articulos
                    // las usa tal cual y la nota por concepto (Web) descarta esos dos campos.
                    Lineas = baseDoc.Lineas.Select(l => new NotaDebitoCompraDetalleCrearDTO
                    {
                        CodArticulo = l.CodArticulo,
                        CodAlmacen = l.CodAlmacen,
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
    }
}
