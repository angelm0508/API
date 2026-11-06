using API.Application.DTO.factura;
using API.Application.DTO.facturaCompra;
using API.Application.DTO.notaDebito;
using API.Application.DTO.notaDebitoCompra;
using API.Application.DTO.socioNegocio;
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

        // ---------------- compras: facturas de compra y notas de débito de proveedor ----------------

        [Fact]
        public void FacturaCompra_CrearDto_MapeaLaCondicionPedidaYNoTocaVencimientoNiNavegacion()
        {
            var entidad = _mapper.Map<FacturaCompra>(new FacturaCompraCrearDTO { Serie = 4, CodigoSn = "P001", CondicionPago = "C30" });

            Assert.Equal("C30", entidad.CondicionPago);
            Assert.Null(entidad.FechaVencimiento);
            Assert.Null(entidad.CondicionPagoNavigation);
        }

        [Fact]
        public void NotaDebitoCompra_CrearDto_MapeaLaCondicionPedidaYNoTocaVencimientoNiNavegacion()
        {
            var entidad = _mapper.Map<FacturaCompra>(new NotaDebitoCompraCrearDTO { Serie = 4, CodigoSn = "P001", CondicionPago = "C15" });

            Assert.Equal("C15", entidad.CondicionPago);
            Assert.Null(entidad.FechaVencimiento);
            Assert.Null(entidad.CondicionPagoNavigation);
        }

        [Fact]
        public void LosDtosDeCompraDeCreacionYActualizacion_NoExponenFechaVencimiento()
        {
            Assert.Null(typeof(FacturaCompraCrearDTO).GetProperty(nameof(FacturaCompra.FechaVencimiento)));
            Assert.Null(typeof(NotaDebitoCompraCrearDTO).GetProperty(nameof(FacturaCompra.FechaVencimiento)));
            Assert.Null(typeof(FacturaCompraActualizarDTO).GetProperty(nameof(FacturaCompra.FechaVencimiento)));
            Assert.Null(typeof(NotaDebitoCompraActualizarDTO).GetProperty(nameof(FacturaCompra.FechaVencimiento)));
            Assert.Null(typeof(FacturaCompraActualizarDTO).GetProperty(nameof(FacturaCompra.CondicionPago)));
            Assert.Null(typeof(NotaDebitoCompraActualizarDTO).GetProperty(nameof(FacturaCompra.CondicionPago)));
        }

        [Fact]
        public void ActualizarDtoDeCompra_NoCambiaCondicionNiVencimientoDeLaEntidad()
        {
            var factura = new FacturaCompra { CondicionPago = "C30", FechaVencimiento = new DateTime(2026, 3, 2) };
            var nota = new FacturaCompra { CondicionPago = "C45", FechaVencimiento = new DateTime(2026, 4, 2) };

            _mapper.Map(new FacturaCompraActualizarDTO { Comentario = "x" }, factura);
            _mapper.Map(new NotaDebitoCompraActualizarDTO { Comentario = "x" }, nota);

            Assert.Equal("C30", factura.CondicionPago);
            Assert.Equal(new DateTime(2026, 3, 2), factura.FechaVencimiento);
            Assert.Equal("C45", nota.CondicionPago);
            Assert.Equal(new DateTime(2026, 4, 2), nota.FechaVencimiento);
        }

        [Fact]
        public void EntidadDeCompraADto_ExponeCondicionYVencimiento()
        {
            var entidad = new FacturaCompra { Entry = 9, CondicionPago = "C30", FechaVencimiento = new DateTime(2026, 3, 2) };

            var factura = _mapper.Map<FacturaCompraDTO>(entidad);
            var nota = _mapper.Map<NotaDebitoCompraDTO>(entidad);

            Assert.Equal("C30", factura.CondicionPago);
            Assert.Equal(new DateTime(2026, 3, 2), factura.FechaVencimiento);
            Assert.Equal("C30", nota.CondicionPago);
            Assert.Equal(new DateTime(2026, 3, 2), nota.FechaVencimiento);
        }

        // ---------------- socio de negocio ----------------

        [Fact]
        public void Socio_CrearYActualizarDto_MapeanLaCondicionYNoTocanLaNavegacion()
        {
            var nuevo = _mapper.Map<SocioNegocio>(new SocioNegocioCrearDTO { Serie = 9, Nombre = "Proveedor", CondicionPago = "C30" });
            var existente = new SocioNegocio { Codigo = "P001" };
            _mapper.Map(new SocioNegocioActualizarDTO { Serie = 9, Nombre = "Proveedor", CondicionPago = "C45" }, existente);

            Assert.Equal("C30", nuevo.CondicionPago);
            Assert.Null(nuevo.CondicionPagoNavigation);
            Assert.Equal("C45", existente.CondicionPago);
            Assert.Null(existente.CondicionPagoNavigation);
        }

        [Fact]
        public void Socio_EntidadADto_ExponeLaCondicion()
        {
            var dto = _mapper.Map<SocioNegocioDTO>(new SocioNegocio { Codigo = "P001", CondicionPago = "C30" });

            Assert.Equal("C30", dto.CondicionPago);
        }

        [Fact]
        public void Socio_CrearYActualizarDto_MapeanLimiteYBloqueo()
        {
            var nuevo = _mapper.Map<SocioNegocio>(new SocioNegocioCrearDTO { Serie = 9, Nombre = "Cliente", LimiteCredito = 5000.50m, BloquearSiVencido = "S" });
            var existente = new SocioNegocio { Codigo = "C001", LimiteCredito = 100m, BloquearSiVencido = "S" };
            _mapper.Map(new SocioNegocioActualizarDTO { Serie = 9, Nombre = "Cliente", LimiteCredito = null, BloquearSiVencido = "N" }, existente);

            Assert.Equal(5000.50m, nuevo.LimiteCredito);
            Assert.Equal("S", nuevo.BloquearSiVencido);
            Assert.Null(existente.LimiteCredito);
            Assert.Equal("N", existente.BloquearSiVencido);
        }

        [Fact]
        public void Socio_EntidadADto_ExponeLimiteYBloqueo()
        {
            var dto = _mapper.Map<SocioNegocioDTO>(new SocioNegocio { Codigo = "C001", LimiteCredito = 0m, BloquearSiVencido = "S" });
            var sinLimite = _mapper.Map<SocioNegocioDTO>(new SocioNegocio { Codigo = "C002" });

            Assert.Equal(0m, dto.LimiteCredito);
            Assert.Equal("S", dto.BloquearSiVencido);
            Assert.Null(sinLimite.LimiteCredito);
            Assert.Equal("N", sinLimite.BloquearSiVencido);
        }
    }
}
