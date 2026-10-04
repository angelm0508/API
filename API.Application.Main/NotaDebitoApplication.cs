using API.Application.DTO;
using API.Application.DTO.notaDebito;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class NotaDebitoApplication : INotaDebitoApplication
    {
        private readonly INotaDebitoDomain _domain;
        private readonly IMapper _mapper;

        public NotaDebitoApplication(INotaDebitoDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(NotaDebitoCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var nota = _mapper.Map<NotaDebito>(obj);
                var lineas = _mapper.Map<IEnumerable<NotaDebitoDetalle>>(obj.Lineas);
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

        public async Task<Respuesta<bool>> ActualizarAsync(int id, NotaDebitoActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var nota = _mapper.Map<NotaDebito>(obj);
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

        public async Task<Respuesta<NotaDebitoDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<NotaDebitoDTO>();
            try
            {
                var nota = await _domain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<NotaDebitoDTO>(nota);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<NotaDebitoDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<NotaDebitoDTO>>();
            try
            {
                var queryable = await _domain.ObtenerTodoAsync();
                var notas = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<NotaDebitoDTO>>(notas);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<NotaDebitoDetalleDTO>>> ObtenerLineasAsync(int id)
        {
            var respuesta = new Respuesta<IEnumerable<NotaDebitoDetalleDTO>>();
            try
            {
                var lineas = await _domain.ObtenerLineasAsync(id);
                respuesta.Dato = _mapper.Map<IEnumerable<NotaDebitoDetalleDTO>>(lineas);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<NotaDebitoFacturaBaseResumenDTO>>> ObtenerFacturasBaseAsync(string codigoSn)
        {
            var respuesta = new Respuesta<IEnumerable<NotaDebitoFacturaBaseResumenDTO>>();
            try
            {
                var facturas = await _domain.ObtenerFacturasBaseAsync(codigoSn);
                respuesta.Dato = facturas.Select(f => new NotaDebitoFacturaBaseResumenDTO
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

        public async Task<Respuesta<NotaDebitoFacturaBaseDTO>> ObtenerFacturaBaseAsync(int entry)
        {
            var respuesta = new Respuesta<NotaDebitoFacturaBaseDTO>();
            try
            {
                var baseDoc = await _domain.ObtenerFacturaBaseAsync(entry);
                respuesta.Dato = new NotaDebitoFacturaBaseDTO
                {
                    Entry = baseDoc.Documento.Entry,
                    NumDoc = baseDoc.Documento.NumDoc,
                    CodigoSn = baseDoc.Documento.CodigoSn,
                    NombreSn = baseDoc.Documento.NombreSn,
                    MonedaDoc = baseDoc.Documento.MonedaDoc,
                    Direccion = baseDoc.Documento.Direccion,
                    // Las lineas de la factura se convierten en conceptos: sin articulo ni almacen.
                    Lineas = baseDoc.Lineas.Select(l => new NotaDebitoDetalleCrearDTO
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
    }
}
