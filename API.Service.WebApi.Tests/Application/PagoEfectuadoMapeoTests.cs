using API.Application.DTO.pagoEfectuado;
using API.Domain.Entity.Models;
using API.Transversal.Mapper;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class PagoEfectuadoMapeoTests
    {
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();

        [Fact]
        public void CrearDtoAEntidad_NoLlenaLasColeccionesDeNavegacion()
        {
            // Los hijos se insertan explicitamente en PagoEfectuadoDomain.InsertarAsync; si el mapeo
            // tambien los dejara en las colecciones de navegacion, EF los insertaria dos veces.
            var dto = new PagoEfectuadoCrearDTO
            {
                Serie = 40, NumDoc = 5, CodigoSn = "P001", Comentario = "pago",
                Facturas =
                {
                    new PagoEfectuadoFacturaCrearDTO { FacturaCompraEntry = 10, MontoAplicado = 100m },
                    new PagoEfectuadoFacturaCrearDTO { FacturaCompraEntry = 11, MontoAplicado = 50m }
                },
                Medios =
                {
                    new PagoEfectuadoMedioCrearDTO { CodFormaPago = "EFE", Monto = 100m },
                    new PagoEfectuadoMedioCrearDTO { CodFormaPago = "CHQ", Monto = 50m, Referencia = "123", Banco = "Banco X" }
                }
            };

            var entidad = _mapper.Map<PagoEfectuado>(dto);

            Assert.Equal(40, entidad.Serie);
            Assert.Equal("P001", entidad.CodigoSn);
            Assert.Equal("pago", entidad.Comentario);
            Assert.Empty(entidad.Facturas);
            Assert.Empty(entidad.Medios);
        }

        [Fact]
        public void FacturaCrearDtoAEntidad_MapeaTodosLosCampos()
        {
            var entidad = _mapper.Map<PagoEfectuadoFactura>(new PagoEfectuadoFacturaCrearDTO { FacturaCompraEntry = 10, MontoAplicado = 99.5m });

            Assert.Equal(10, entidad.FacturaCompraEntry);
            Assert.Equal(99.5m, entidad.MontoAplicado);
        }

        [Fact]
        public void MedioCrearDtoAEntidad_MapeaTodosLosCampos()
        {
            var fecha = new DateTime(2026, 10, 25);
            var entidad = _mapper.Map<PagoEfectuadoMedio>(new PagoEfectuadoMedioCrearDTO
            {
                CodFormaPago = "CHQ", Monto = 75m, Referencia = "REF-1", Banco = "Banco X", FechaCheque = fecha
            });

            Assert.Equal("CHQ", entidad.CodFormaPago);
            Assert.Equal(75m, entidad.Monto);
            Assert.Equal("REF-1", entidad.Referencia);
            Assert.Equal("Banco X", entidad.Banco);
            Assert.Equal(fecha, entidad.FechaCheque);
        }
    }
}
