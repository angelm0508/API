using API.Application.DTO.factura;
using API.Application.DTO.notaDebito;
using API.Domain.Entity.Models;
using API.Transversal.Mapper;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    /// <summary>Condición de pago y vencimiento en facturas y notas de débito de cliente: qué copia AutoMapper y qué no.</summary>
    public class VencimientoMapeoTests
    {
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();

        [Fact]
        public void Factura_CrearDto_MapeaLaCondicionPedidaYNoTocaVencimientoNiNavegacion()
        {
            var entidad = _mapper.Map<Factura>(new FacturaCrearDTO { Serie = 4, CodigoSn = "C001", CondicionPago = "C30" });

            Assert.Equal("C30", entidad.CondicionPago);
            Assert.Null(entidad.FechaVencimiento);
            Assert.Null(entidad.CondicionPagoNavigation);
        }

        [Fact]
        public void NotaDebito_CrearDto_MapeaLaCondicionPedidaYNoTocaVencimientoNiNavegacion()
        {
            var entidad = _mapper.Map<Factura>(new NotaDebitoCrearDTO { Serie = 4, CodigoSn = "C001", CondicionPago = "C15" });

            Assert.Equal("C15", entidad.CondicionPago);
            Assert.Null(entidad.FechaVencimiento);
            Assert.Null(entidad.CondicionPagoNavigation);
        }

        [Fact]
        public void LosDtosDeCreacionYActualizacion_NoExponenFechaVencimiento()
        {
            // El cliente no puede fijar el vencimiento: no existe en ningún DTO de entrada.
            Assert.Null(typeof(FacturaCrearDTO).GetProperty(nameof(Factura.FechaVencimiento)));
            Assert.Null(typeof(NotaDebitoCrearDTO).GetProperty(nameof(Factura.FechaVencimiento)));
            Assert.Null(typeof(FacturaActualizarDTO).GetProperty(nameof(Factura.FechaVencimiento)));
            Assert.Null(typeof(NotaDebitoActualizarDTO).GetProperty(nameof(Factura.FechaVencimiento)));
            Assert.Null(typeof(FacturaActualizarDTO).GetProperty(nameof(Factura.CondicionPago)));
            Assert.Null(typeof(NotaDebitoActualizarDTO).GetProperty(nameof(Factura.CondicionPago)));
        }

        [Fact]
        public void ActualizarDto_NoCambiaCondicionNiVencimientoDeLaEntidad()
        {
            var factura = new Factura { CondicionPago = "C30", FechaVencimiento = new DateTime(2026, 3, 2) };
            var nota = new Factura { CondicionPago = "C45", FechaVencimiento = new DateTime(2026, 4, 2) };

            _mapper.Map(new FacturaActualizarDTO { Comentario = "x" }, factura);
            _mapper.Map(new NotaDebitoActualizarDTO { Comentario = "x" }, nota);

            Assert.Equal("C30", factura.CondicionPago);
            Assert.Equal(new DateTime(2026, 3, 2), factura.FechaVencimiento);
            Assert.Equal("C45", nota.CondicionPago);
            Assert.Equal(new DateTime(2026, 4, 2), nota.FechaVencimiento);
        }

        [Fact]
        public void EntidadADto_ExponeCondicionYVencimiento()
        {
            var entidad = new Factura { Entry = 9, CondicionPago = "C30", FechaVencimiento = new DateTime(2026, 3, 2) };

            var factura = _mapper.Map<FacturaDTO>(entidad);
            var nota = _mapper.Map<NotaDebitoDTO>(entidad);

            Assert.Equal("C30", factura.CondicionPago);
            Assert.Equal(new DateTime(2026, 3, 2), factura.FechaVencimiento);
            Assert.Equal("C30", nota.CondicionPago);
            Assert.Equal(new DateTime(2026, 3, 2), nota.FechaVencimiento);
        }
    }
}
