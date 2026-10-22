using API.Application.DTO.ordenServicio;
using API.Application.Main;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Transversal.Mapper;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class OrdenServicioDetalleApplicationTests
    {
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();
        private readonly Mock<IOrdenServicioDetalleDomain> _domain = new();
        private readonly OrdenServicioDetalleApplication _app;

        public OrdenServicioDetalleApplicationTests()
        {
            _app = new OrdenServicioDetalleApplication(_domain.Object, _mapper);
        }

        [Fact]
        public async Task Insertar_MapeaYDevuelveElNumeroDeLinea()
        {
            OrdenServicioDetalle? recibida = null;
            _domain.Setup(d => d.InsertarAsync(It.IsAny<OrdenServicioDetalle>()))
                .Callback<OrdenServicioDetalle>(l => recibida = l).ReturnsAsync(3);

            var respuesta = await _app.InsertarAsync(new OrdenServicioDetalleCrearDTO { Entry = 50, CodArticulo = "ACE-1", Cantidad = 2, Precio = 85m, CodigoImpuesto = "IVA" });

            Assert.True(respuesta.Resultado);
            Assert.Equal(3, respuesta.Dato);
            Assert.Equal("Línea agregada correctamente.", respuesta.Mensaje);
            Assert.Equal((50, "ACE-1", 2m), (recibida!.Entry, recibida.CodArticulo, recibida.Cantidad));
        }

        [Fact]
        public async Task Insertar_ErrorDelDominio_DevuelveElMensaje()
        {
            _domain.Setup(d => d.InsertarAsync(It.IsAny<OrdenServicioDetalle>())).ThrowsAsync(new Exception("La orden de servicio no existe."));

            var respuesta = await _app.InsertarAsync(new OrdenServicioDetalleCrearDTO());

            Assert.False(respuesta.Resultado);
            Assert.Equal("La orden de servicio no existe.", respuesta.Mensaje);
        }

        [Fact]
        public async Task Actualizar_LineaInexistente_DevuelveResultadoFalse()
        {
            _domain.Setup(d => d.ActualizarAsync(50, 9, It.IsAny<OrdenServicioDetalle>())).ReturnsAsync(false);

            var respuesta = await _app.ActualizarAsync(50, 9, new OrdenServicioDetalleActualizarDTO());

            Assert.False(respuesta.Resultado);
            Assert.Equal("La línea no se encontró.", respuesta.Mensaje);
        }

        [Fact]
        public async Task Actualizar_Ok()
        {
            _domain.Setup(d => d.ActualizarAsync(50, 1, It.Is<OrdenServicioDetalle>(l => l.Cantidad == 3m))).ReturnsAsync(true);

            var respuesta = await _app.ActualizarAsync(50, 1, new OrdenServicioDetalleActualizarDTO { Cantidad = 3m });

            Assert.True(respuesta.Resultado);
            Assert.Equal("Línea actualizada correctamente.", respuesta.Mensaje);
        }

        [Fact]
        public async Task Eliminar_Ok_Inexistente_YError()
        {
            _domain.Setup(d => d.EliminarAsync(50, 1)).ReturnsAsync(true);
            _domain.Setup(d => d.EliminarAsync(50, 9)).ReturnsAsync(false);
            _domain.Setup(d => d.EliminarAsync(50, 2)).ThrowsAsync(new Exception("No se puede eliminar la línea 2: la usan Factura 2. Cancele primero esos documentos."));

            var ok = await _app.EliminarAsync(50, 1);
            var noExiste = await _app.EliminarAsync(50, 9);
            var error = await _app.EliminarAsync(50, 2);

            Assert.True(ok.Resultado);
            Assert.Equal("Línea eliminada correctamente.", ok.Mensaje);
            Assert.False(noExiste.Resultado);
            Assert.Equal("La línea no se encontró.", noExiste.Mensaje);
            Assert.False(error.Resultado);
            Assert.StartsWith("No se puede eliminar la línea 2", error.Mensaje);
        }
    }
}
