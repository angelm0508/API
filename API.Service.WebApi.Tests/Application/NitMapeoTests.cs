using API.Application.DTO.cotizacion;
using API.Application.DTO.entrega;
using API.Application.DTO.entregaCompra;
using API.Application.DTO.factura;
using API.Application.DTO.facturaCompra;
using API.Application.DTO.notaCredito;
using API.Application.DTO.notaCreditoCompra;
using API.Application.DTO.notaDebito;
using API.Application.DTO.notaDebitoCompra;
using API.Application.DTO.pedido;
using API.Application.DTO.pedidoCompra;
using API.Domain.Entity.Models;
using API.Transversal.Mapper;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    /// <summary>
    /// El NIT es una copia del socio que se fija al crear: viaja en los DTO de creación y de consulta de los once documentos
    /// (nueve entidades; las notas de débito comparten la entidad de la factura) y los DTO de actualización no lo traen,
    /// así que un mapeo de actualización sobre la entidad existente lo conserva.
    /// </summary>
    public class NitMapeoTests
    {
        private const string Nit = "1234567-8";
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();

        // (DTO de creación, entidad, DTO de consulta, DTO de actualización) de cada documento.
        public static IEnumerable<object[]> Documentos() => new[]
        {
            new object[] { typeof(CotizacionCrearDTO), typeof(Cotizacion), typeof(CotizacionDTO), typeof(CotizacionActualizarDTO) },
            new object[] { typeof(PedidoCrearDTO), typeof(Pedido), typeof(PedidoDTO), typeof(PedidoActualizarDTO) },
            new object[] { typeof(EntregaCrearDTO), typeof(Entrega), typeof(EntregaDTO), typeof(EntregaActualizarDTO) },
            new object[] { typeof(FacturaCrearDTO), typeof(Factura), typeof(FacturaDTO), typeof(FacturaActualizarDTO) },
            new object[] { typeof(NotaCreditoCrearDTO), typeof(NotaCredito), typeof(NotaCreditoDTO), typeof(NotaCreditoActualizarDTO) },
            new object[] { typeof(NotaDebitoCrearDTO), typeof(Factura), typeof(NotaDebitoDTO), typeof(NotaDebitoActualizarDTO) },
            new object[] { typeof(PedidoCompraCrearDTO), typeof(PedidoCompra), typeof(PedidoCompraDTO), typeof(PedidoCompraActualizarDTO) },
            new object[] { typeof(EntregaCompraCrearDTO), typeof(EntregaCompra), typeof(EntregaCompraDTO), typeof(EntregaCompraActualizarDTO) },
            new object[] { typeof(FacturaCompraCrearDTO), typeof(FacturaCompra), typeof(FacturaCompraDTO), typeof(FacturaCompraActualizarDTO) },
            new object[] { typeof(NotaCreditoCompraCrearDTO), typeof(NotaCreditoCompra), typeof(NotaCreditoCompraDTO), typeof(NotaCreditoCompraActualizarDTO) },
            new object[] { typeof(NotaDebitoCompraCrearDTO), typeof(FacturaCompra), typeof(NotaDebitoCompraDTO), typeof(NotaDebitoCompraActualizarDTO) },
        };

        private static string? LeerNit(object o) => (string?)o.GetType().GetProperty("Nit")!.GetValue(o);
        private static void PonerNit(object o, string? valor) => o.GetType().GetProperty("Nit")!.SetValue(o, valor);

        [Theory]
        [MemberData(nameof(Documentos))]
        public void CrearDto_TraeNit_YSeMapeaALaEntidad(Type crear, Type entidad, Type consulta, Type actualizar)
        {
            var dto = Activator.CreateInstance(crear)!;
            PonerNit(dto, Nit);

            var mapeada = _mapper.Map(dto, crear, entidad);

            Assert.Equal(Nit, LeerNit(mapeada));
        }

        [Theory]
        [MemberData(nameof(Documentos))]
        public void Entidad_SeMapeaAlDtoDeConsultaConNit(Type crear, Type entidad, Type consulta, Type actualizar)
        {
            var doc = Activator.CreateInstance(entidad)!;
            PonerNit(doc, Nit);

            var dto = _mapper.Map(doc, entidad, consulta);

            Assert.Equal(Nit, LeerNit(dto));
        }

        [Theory]
        [MemberData(nameof(Documentos))]
        public void ActualizarDto_NoTraeNit_YElMapeoSobreLaEntidadExistenteLoConserva(Type crear, Type entidad, Type consulta, Type actualizar)
        {
            Assert.Null(actualizar.GetProperty("Nit"));   // el NIT no cambia tras crear

            var existente = Activator.CreateInstance(entidad)!;
            PonerNit(existente, Nit);

            _mapper.Map(Activator.CreateInstance(actualizar)!, existente, actualizar, entidad);

            Assert.Equal(Nit, LeerNit(existente));
        }
    }
}
