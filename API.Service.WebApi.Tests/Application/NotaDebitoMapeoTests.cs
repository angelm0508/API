using API.Application.DTO.notaDebito;
using API.Application.DTO.notaDebitoCompra;
using API.Domain.Entity.Models;
using API.Transversal.Mapper;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class NotaDebitoMapeoTests
    {
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();

        [Fact]
        public void Cliente_CrearDtoAEntidad_MapeaEncabezadoYIgnoraLineas()
        {
            var dto = new NotaDebitoCrearDTO
            {
                NumDoc = 7, Serie = 33, CodigoSn = "C001", NombreSn = "Cliente", MonedaDoc = "QTZ",
                BaseTipo = 6, BaseEntry = 12, TotalDoc = 112m, Comentario = "ajuste",
                Lineas = { new NotaDebitoDetalleCrearDTO { Descripcion = "Flete", Cantidad = 1, Precio = 100m, CodigoImpuesto = "IVA" } }
            };

            var entidad = _mapper.Map<Factura>(dto);

            Assert.Equal(33, entidad.Serie);
            Assert.Equal("C001", entidad.CodigoSn);
            Assert.Equal(6, entidad.BaseTipo);
            Assert.Equal(12, entidad.BaseEntry);
            Assert.Equal(112m, entidad.TotalDoc);
        }

        [Fact]
        public void CrearDtoSinTipoDeNota_NoSeConvierteEnArticulos()
        {
            // Un tipo de nota ausente debe llegar nulo al dominio (que lo rechaza), nunca como 'I'.
            Assert.Null(_mapper.Map<Factura>(new NotaDebitoCrearDTO()).DocType);
            Assert.Null(_mapper.Map<FacturaCompra>(new NotaDebitoCompraCrearDTO()).DocType);
        }

        [Fact]
        public void Cliente_DetalleCrearDtoAEntidad_MapeaConceptos()
        {
            var dto = new NotaDebitoDetalleCrearDTO { Descripcion = "Intereses", Cantidad = 2, Precio = 50m, PrctjeDesc = 5m, CodigoImpuesto = "IVA", Impuesto = 11.4m, TotalLinea = 106.4m };

            var entidad = _mapper.Map<FacturaDetalle>(dto);

            Assert.Equal("Intereses", entidad.Descripcion);
            Assert.Equal(2m, entidad.Cantidad);
            Assert.Equal("IVA", entidad.CodigoImpuesto);
            Assert.Equal(106.4m, entidad.TotalLinea);
        }

        [Fact]
        public void Cliente_EntidadADto_MapeaEncabezadoYDetalle()
        {
            var nota = _mapper.Map<NotaDebitoDTO>(new Factura { Entry = 9, NumDoc = 3, Serie = 33, CodigoSn = "C001", TotalDoc = 10m, Cancelado = "N" });
            var linea = _mapper.Map<NotaDebitoDetalleDTO>(new FacturaDetalle { Entry = 9, NoLinea = 2, Descripcion = "X", CodigoImpuesto = "IVA" });

            Assert.Equal(9, nota.Entry);
            Assert.Equal("C001", nota.CodigoSn);
            Assert.Equal(2, linea.NoLinea);
            Assert.Equal("X", linea.Descripcion);
        }

        [Fact]
        public void Proveedor_Mapeos_EquivalentesAlCliente()
        {
            var entidad = _mapper.Map<FacturaCompra>(new NotaDebitoCompraCrearDTO { Serie = 35, CodigoSn = "P001", BaseTipo = 13, BaseEntry = 4 });
            var detalle = _mapper.Map<FacturaCompraDetalle>(new NotaDebitoCompraDetalleCrearDTO { Descripcion = "Diferencia de precio", CodigoImpuesto = "EXE" });
            var dto = _mapper.Map<NotaDebitoCompraDTO>(new FacturaCompra { Entry = 1, CodigoSn = "P001" });
            var lineaDto = _mapper.Map<NotaDebitoCompraDetalleDTO>(new FacturaCompraDetalle { Entry = 1, NoLinea = 1, Descripcion = "Z", CodigoImpuesto = "EXE" });

            Assert.Equal(13, entidad.BaseTipo);
            Assert.Equal("Diferencia de precio", detalle.Descripcion);
            Assert.Equal("P001", dto.CodigoSn);
            Assert.Equal("Z", lineaDto.Descripcion);
        }

        [Fact]
        public void Cliente_MapeaElModoYElArticuloEnAmbosSentidos()
        {
            var entidad = _mapper.Map<Factura>(new NotaDebitoCrearDTO { DocType = "I", Serie = 33 });
            var linea = _mapper.Map<FacturaDetalle>(new NotaDebitoDetalleCrearDTO { CodArticulo = "ART1", CodAlmacen = "01", CodigoImpuesto = "IVA" });
            var dto = _mapper.Map<NotaDebitoDTO>(new Factura { Entry = 4, DocType = "S", DocSubType = "ND" });
            var lineaDto = _mapper.Map<NotaDebitoDetalleDTO>(new FacturaDetalle { Entry = 4, NoLinea = 1, CodArticulo = "ART1", CodAlmacen = "01" });

            Assert.Equal("I", entidad.DocType);
            Assert.Equal(("ART1", "01"), (linea.CodArticulo, linea.CodAlmacen));
            Assert.Equal("S", dto.DocType);
            Assert.Equal(("ART1", "01"), (lineaDto.CodArticulo, lineaDto.CodAlmacen));
        }

        [Fact]
        public void Proveedor_MapeaElModoYElArticuloEnAmbosSentidos()
        {
            var entidad = _mapper.Map<FacturaCompra>(new NotaDebitoCompraCrearDTO { DocType = "I", Serie = 35 });
            var linea = _mapper.Map<FacturaCompraDetalle>(new NotaDebitoCompraDetalleCrearDTO { CodArticulo = "ART1", CodAlmacen = "01", CodigoImpuesto = "EXE" });
            var dto = _mapper.Map<NotaDebitoCompraDTO>(new FacturaCompra { Entry = 4, DocType = "S", DocSubType = "DN" });
            var lineaDto = _mapper.Map<NotaDebitoCompraDetalleDTO>(new FacturaCompraDetalle { Entry = 4, NoLinea = 1, CodArticulo = "ART1", CodAlmacen = "01" });

            Assert.Equal("I", entidad.DocType);
            Assert.Equal(("ART1", "01"), (linea.CodArticulo, linea.CodAlmacen));
            Assert.Equal("S", dto.DocType);
            Assert.Equal(("ART1", "01"), (lineaDto.CodArticulo, lineaDto.CodAlmacen));
        }
    }
}
