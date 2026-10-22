using API.Application.DTO.ordenServicio;
using API.Application.Main;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using API.Transversal.Mapper;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class OrdenServicioApplicationTests
    {
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();
        private readonly Mock<IOrdenServicioDomain> _domain = new();
        private readonly OrdenServicioApplication _app;

        public OrdenServicioApplicationTests()
        {
            _app = new OrdenServicioApplication(_domain.Object, _mapper);
        }

        [Fact]
        public async Task Insertar_MapeaCabeceraYLineas_YDevuelveElEntry()
        {
            OrdenServicio? recibida = null;
            List<OrdenServicioDetalle>? lineasRecibidas = null;
            _domain.Setup(d => d.InsertarAsync(It.IsAny<OrdenServicio>(), It.IsAny<IEnumerable<OrdenServicioDetalle>>()))
                .Callback<OrdenServicio, IEnumerable<OrdenServicioDetalle>>((o, l) => { recibida = o; lineasRecibidas = l.ToList(); })
                .ReturnsAsync(42);
            var dto = new OrdenServicioCrearDTO
            {
                Serie = 33, CodigoSn = "SN1", MonedaDoc = "QTZ", MotoEntry = 10, CodMecanico = "M01", KmIngreso = 12000, FallaReportada = "No enciende",
                Lineas =
                {
                    new OrdenServicioDetalleCrearDTO { CodArticulo = "ACE-1", Cantidad = 2, Precio = 85m, CodigoImpuesto = "IVA", CodAlmacen = "01" },
                    new OrdenServicioDetalleCrearDTO { CodArticulo = "MO-1", Cantidad = 1, Precio = 150m, CodigoImpuesto = "IVA" }
                }
            };

            var respuesta = await _app.InsertarAsync(dto);

            Assert.True(respuesta.Resultado);
            Assert.Equal(42, respuesta.Dato);
            Assert.Equal("Registro agregado correctamente.", respuesta.Mensaje);
            Assert.Equal(("SN1", 10, "M01", 12000, "No enciende", 33), (recibida!.CodigoSn, recibida.MotoEntry, recibida.CodMecanico, recibida.KmIngreso, recibida.FallaReportada, recibida.Serie));
            Assert.Equal(new[] { "ACE-1", "MO-1" }, lineasRecibidas!.Select(l => l.CodArticulo));
            Assert.Equal("01", lineasRecibidas![0].CodAlmacen);
        }

        [Fact]
        public async Task Insertar_ErrorDelDominio_DevuelveResultadoFalseConElMensaje()
        {
            _domain.Setup(d => d.InsertarAsync(It.IsAny<OrdenServicio>(), It.IsAny<IEnumerable<OrdenServicioDetalle>>()))
                .ThrowsAsync(new Exception("La serie no existe."));

            var respuesta = await _app.InsertarAsync(new OrdenServicioCrearDTO());

            Assert.False(respuesta.Resultado);
            Assert.Equal("La serie no existe.", respuesta.Mensaje);
        }

        [Fact]
        public async Task Obtener_MapeaPlacaMotoYMecanico()
        {
            _domain.Setup(d => d.ObtenerAsync(5)).ReturnsAsync(new OrdenServicio
            {
                Entry = 5, EstadoServicio = "P",
                MotoNavigation = new Motocicleta { Placa = "M123ABC", Marca = "Honda", Modelo = "CB125" },
                MecanicoNavigation = new Mecanico { Nombre = "Juan" }
            });

            var respuesta = await _app.ObtenerAsync(5);

            Assert.True(respuesta.Resultado);
            Assert.Equal(("M123ABC", "Honda CB125", "Juan", "P"),
                (respuesta.Dato.Placa, respuesta.Dato.MotoDescripcion, respuesta.Dato.NombreMecanico, respuesta.Dato.EstadoServicio));
        }

        [Fact]
        public async Task Obtener_SinMotoNiMecanicoCargados_DejaLosNombresEnNulo()
        {
            _domain.Setup(d => d.ObtenerAsync(5)).ReturnsAsync(new OrdenServicio { Entry = 5, EstadoServicio = "R" });

            var respuesta = await _app.ObtenerAsync(5);

            Assert.Null(respuesta.Dato.Placa);
            Assert.Null(respuesta.Dato.MotoDescripcion);
            Assert.Null(respuesta.Dato.NombreMecanico);
        }

        [Fact]
        public async Task Obtener_Inexistente_DevuelveDatoNulo()
        {
            _domain.Setup(d => d.ObtenerAsync(99)).ReturnsAsync((OrdenServicio?)null);

            var respuesta = await _app.ObtenerAsync(99);

            Assert.True(respuesta.Resultado);
            Assert.Null(respuesta.Dato);
        }

        [Fact]
        public async Task ObtenerTodo_FiltraPorEstadoYOrdenaDeLaMasNuevaALaMasAntigua()
        {
            var ordenes = new List<OrdenServicio>
            {
                new() { Entry = 1, EstadoServicio = "T" },
                new() { Entry = 2, EstadoServicio = "R" },
                new() { Entry = 3, EstadoServicio = "T" },
            };
            _domain.Setup(d => d.ObtenerTodoAsync()).ReturnsAsync(ordenes.AsAsyncQueryable());

            var terminadas = await _app.ObtenerTodoAsync("t");
            var todas = await _app.ObtenerTodoAsync(null);

            Assert.Equal(new[] { 3, 1 }, terminadas.Dato.Select(o => o.Entry));
            Assert.Equal(new[] { 3, 2, 1 }, todas.Dato.Select(o => o.Entry));
        }

        [Fact]
        public async Task Actualizar_OrdenInexistente_DevuelveResultadoFalse()
        {
            _domain.Setup(d => d.ActualizarAsync(99, It.IsAny<OrdenServicio>())).ReturnsAsync(false);

            var respuesta = await _app.ActualizarAsync(99, new OrdenServicioActualizarDTO());

            Assert.False(respuesta.Resultado);
            Assert.Equal("La orden de servicio no se encontró.", respuesta.Mensaje);
        }

        [Fact]
        public async Task Actualizar_Ok()
        {
            _domain.Setup(d => d.ActualizarAsync(5, It.Is<OrdenServicio>(o => o.Diagnostico == "Bujía"))).ReturnsAsync(true);

            var respuesta = await _app.ActualizarAsync(5, new OrdenServicioActualizarDTO { Diagnostico = "Bujía" });

            Assert.True(respuesta.Resultado);
            Assert.Equal("Registro actualizado correctamente.", respuesta.Mensaje);
        }

        [Fact]
        public async Task CambiarEstado_Ok_YConErrorDelDominio()
        {
            _domain.Setup(d => d.CambiarEstadoAsync(5, "P")).ReturnsAsync(true);
            _domain.Setup(d => d.CambiarEstadoAsync(5, "R")).ThrowsAsync(new Exception("La orden ya está en estado Recibida."));

            var ok = await _app.CambiarEstadoAsync(5, "P");
            var mala = await _app.CambiarEstadoAsync(5, "R");

            Assert.True(ok.Resultado);
            Assert.Equal("Estado actualizado correctamente.", ok.Mensaje);
            Assert.False(mala.Resultado);
            Assert.Equal("La orden ya está en estado Recibida.", mala.Mensaje);
        }

        [Fact]
        public async Task Cancelar_Ok_YConErrorDelDominio()
        {
            _domain.SetupSequence(d => d.CancelarAsync(5))
                .ReturnsAsync(true)
                .ThrowsAsync(new Exception("No se puede cancelar el documento: lo usan Factura 2. Cancele primero esos documentos."));

            var ok = await _app.CancelarAsync(5);
            var mala = await _app.CancelarAsync(5);

            Assert.True(ok.Resultado);
            Assert.Equal("Orden cancelada. El inventario de los insumos fue devuelto.", ok.Mensaje);
            Assert.False(mala.Resultado);
            Assert.StartsWith("No se puede cancelar el documento", mala.Mensaje);
        }

        [Fact]
        public async Task ObtenerLineas_MapeaLasLineas()
        {
            _domain.Setup(d => d.ObtenerLineasAsync(5)).ReturnsAsync(new[]
            {
                new OrdenServicioDetalle { Entry = 5, NoLinea = 1, CodArticulo = "ACE-1", EsInsumo = "S", Cantidad = 2m, TotalLinea = 190.4m }
            });

            var respuesta = await _app.ObtenerLineasAsync(5);

            var linea = Assert.Single(respuesta.Dato);
            Assert.Equal(("ACE-1", "S", 190.4m), (linea.CodArticulo, linea.EsInsumo, linea.TotalLinea));
        }
    }
}
