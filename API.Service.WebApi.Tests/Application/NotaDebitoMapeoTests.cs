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

            var entidad = _mapper.Map<NotaDebito>(dto);

            Assert.Equal(33, entidad.Serie);
            Assert.Equal("C001", entidad.CodigoSn);
            Assert.Equal(6, entidad.BaseTipo);
            Assert.Equal(12, entidad.BaseEntry);
            Assert.Equal(112m, entidad.TotalDoc);
        }

        [Fact]
        public void Cliente_DetalleCrearDtoAEntidad_MapeaConceptos()
        {
            var dto = new NotaDebitoDetalleCrearDTO { Descripcion = "Intereses", Cantidad = 2, Precio = 50m, PrctjeDesc = 5m, CodigoImpuesto = "IVA", Impuesto = 11.4m, TotalLinea = 106.4m };

            var entidad = _mapper.Map<NotaDebitoDetalle>(dto);

            Assert.Equal("Intereses", entidad.Descripcion);
            Assert.Equal(2m, entidad.Cantidad);
            Assert.Equal("IVA", entidad.CodigoImpuesto);
            Assert.Equal(106.4m, entidad.TotalLinea);
        }

        [Fact]
        public void Cliente_EntidadADto_MapeaEncabezadoYDetalle()
        {
            var nota = _mapper.Map<NotaDebitoDTO>(new NotaDebito { Entry = 9, NumDoc = 3, Serie = 33, CodigoSn = "C001", TotalDoc = 10m, Cancelado = "N" });
            var linea = _mapper.Map<NotaDebitoDetalleDTO>(new NotaDebitoDetalle { Entry = 9, NoLinea = 2, Descripcion = "X", CodigoImpuesto = "IVA" });

            Assert.Equal(9, nota.Entry);
            Assert.Equal("C001", nota.CodigoSn);
            Assert.Equal(2, linea.NoLinea);
            Assert.Equal("X", linea.Descripcion);
        }

        [Fact]
        public void Proveedor_Mapeos_EquivalentesAlCliente()
        {
            var entidad = _mapper.Map<NotaDebitoCompra>(new NotaDebitoCompraCrearDTO { Serie = 35, CodigoSn = "P001", BaseTipo = 13, BaseEntry = 4 });
            var detalle = _mapper.Map<NotaDebitoCompraDetalle>(new NotaDebitoCompraDetalleCrearDTO { Descripcion = "Diferencia de precio", CodigoImpuesto = "EXE" });
            var dto = _mapper.Map<NotaDebitoCompraDTO>(new NotaDebitoCompra { Entry = 1, CodigoSn = "P001" });
            var lineaDto = _mapper.Map<NotaDebitoCompraDetalleDTO>(new NotaDebitoCompraDetalle { Entry = 1, NoLinea = 1, Descripcion = "Z", CodigoImpuesto = "EXE" });

            Assert.Equal(13, entidad.BaseTipo);
            Assert.Equal("Diferencia de precio", detalle.Descripcion);
            Assert.Equal("P001", dto.CodigoSn);
            Assert.Equal("Z", lineaDto.Descripcion);
        }
    }
}
