using API.Application.DTO.notaCreditoCompra;
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
    public class NotaCreditoCompraApplicationTests
    {
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();
        private readonly Mock<INotaCreditoCompraDomain> _domain = new();
        private readonly NotaCreditoCompraApplication _app;

        public NotaCreditoCompraApplicationTests()
        {
            _app = new NotaCreditoCompraApplication(_domain.Object, _mapper);
        }

        [Fact]
        public async Task Insertar_MapeaEncabezadoYLineas_YDevuelveElEntry()
        {
            NotaCreditoCompra? recibida = null;
            List<NotaCreditoCompraDetalle>? lineasRecibidas = null;
            _domain.Setup(d => d.InsertarAsync(It.IsAny<NotaCreditoCompra>(), It.IsAny<IEnumerable<NotaCreditoCompraDetalle>>()))
                .Callback<NotaCreditoCompra, IEnumerable<NotaCreditoCompraDetalle>>((n, l) => { recibida = n; lineasRecibidas = l.ToList(); })
                .ReturnsAsync(42);
            var dto = new NotaCreditoCompraCrearDTO
            {
                Serie = 33, CodigoSn = "SN1", MonedaDoc = "QTZ", Comentario = "c",
                Lineas = { new NotaCreditoCompraDetalleCrearDTO { Descripcion = "Flete", Cantidad = 2, Precio = 10m, CodigoImpuesto = "IVA" } }
            };

            var respuesta = await _app.InsertarAsync(dto);

            Assert.True(respuesta.Resultado);
            Assert.Equal(42, respuesta.Dato);
            Assert.Equal("Registro agregado correctamente.", respuesta.Mensaje);
            Assert.Equal("SN1", recibida!.CodigoSn);
            var linea = Assert.Single(lineasRecibidas!);
            Assert.Equal("Flete", linea.Descripcion);
            Assert.Equal(2m, linea.Cantidad);
        }

        [Fact]
        public async Task Insertar_ErrorDelDominio_DevuelveResultadoFalseConElMensaje()
        {
            _domain.Setup(d => d.InsertarAsync(It.IsAny<NotaCreditoCompra>(), It.IsAny<IEnumerable<NotaCreditoCompraDetalle>>()))
                .ThrowsAsync(new Exception("La serie no existe."));

            var respuesta = await _app.InsertarAsync(new NotaCreditoCompraCrearDTO());

            Assert.False(respuesta.Resultado);
            Assert.Equal("La serie no existe.", respuesta.Mensaje);
        }

        [Fact]
        public async Task Actualizar_DominioDevuelveTrue_ResultadoTrue()
        {
            _domain.Setup(d => d.ActualizarAsync(5, It.IsAny<NotaCreditoCompra>())).ReturnsAsync(true);

            var respuesta = await _app.ActualizarAsync(5, new NotaCreditoCompraActualizarDTO { Cancelado = "S" });

            Assert.True(respuesta.Resultado);
            Assert.True(respuesta.Dato);
            Assert.Equal("Registro actualizado correctamente.", respuesta.Mensaje);
        }

        [Fact]
        public async Task Actualizar_DominioDevuelveFalse_ResultadoFalse()
        {
            _domain.Setup(d => d.ActualizarAsync(5, It.IsAny<NotaCreditoCompra>())).ReturnsAsync(false);

            var respuesta = await _app.ActualizarAsync(5, new NotaCreditoCompraActualizarDTO());

            Assert.False(respuesta.Resultado);
        }

        [Fact]
        public async Task Actualizar_ErrorDelDominio_DevuelveElMensaje()
        {
            _domain.Setup(d => d.ActualizarAsync(5, It.IsAny<NotaCreditoCompra>()))
                .ThrowsAsync(new Exception("El documento está cancelado y no se puede modificar."));

            var respuesta = await _app.ActualizarAsync(5, new NotaCreditoCompraActualizarDTO());

            Assert.False(respuesta.Resultado);
            Assert.Equal("El documento está cancelado y no se puede modificar.", respuesta.Mensaje);
        }

        [Fact]
        public async Task Eliminar_Ok_Y_ErrorDelDominio()
        {
            _domain.Setup(d => d.EliminarAsync(5)).ReturnsAsync(true);
            _domain.Setup(d => d.EliminarAsync(6)).ThrowsAsync(new Exception("Cancele el documento (Cancelado='S') antes de eliminarlo."));

            var ok = await _app.EliminarAsync(5);
            var error = await _app.EliminarAsync(6);

            Assert.True(ok.Resultado);
            Assert.Equal("Registro eliminado correctamente.", ok.Mensaje);
            Assert.False(error.Resultado);
            Assert.Equal("Cancele el documento (Cancelado='S') antes de eliminarlo.", error.Mensaje);
        }

        [Fact]
        public async Task Obtener_MapeaElEncabezado()
        {
            _domain.Setup(d => d.ObtenerAsync(5)).ReturnsAsync(new NotaCreditoCompra { Entry = 5, NumDoc = 9, CodigoSn = "SN1" });

            var respuesta = await _app.ObtenerAsync(5);

            Assert.True(respuesta.Resultado);
            Assert.Equal(9, respuesta.Dato.NumDoc);
        }

        [Fact]
        public async Task ObtenerTodo_MapeaTodos()
        {
            var lista = new List<NotaCreditoCompra> { new() { Entry = 1 }, new() { Entry = 2 } };
            _domain.Setup(d => d.ObtenerTodoAsync()).ReturnsAsync(lista.AsAsyncQueryable());

            var respuesta = await _app.ObtenerTodoAsync();

            Assert.True(respuesta.Resultado);
            Assert.Equal(new[] { 1, 2 }, respuesta.Dato.Select(n => n.Entry));
        }

        [Fact]
        public async Task ObtenerLineas_MapeaLasLineas()
        {
            _domain.Setup(d => d.ObtenerLineasAsync(5)).ReturnsAsync(new List<NotaCreditoCompraDetalle>
            {
                new() { Entry = 5, NoLinea = 1, Descripcion = "A", CodigoImpuesto = "IVA" }
            });

            var respuesta = await _app.ObtenerLineasAsync(5);

            Assert.True(respuesta.Resultado);
            Assert.Equal("A", Assert.Single(respuesta.Dato).Descripcion);
        }

        [Fact]
        public async Task ObtenerFacturasCompraBase_MapeaElSubtipoDelDocumento()
        {
            _domain.Setup(d => d.ObtenerFacturasCompraBaseAsync("SN1")).ReturnsAsync(new List<FacturaCompraAcreditable>
            {
                new(new FacturaCompra { Entry = 12, NumDoc = 100, TotalDoc = 112m, MonedaDoc = "QTZ", CodigoSn = "SN1", DocSubType = "--" }, 0m),
                new(new FacturaCompra { Entry = 13, NumDoc = 7, TotalDoc = 50m, MonedaDoc = "QTZ", CodigoSn = "SN1", DocSubType = "DN" }, 0m)
            });

            var lista = (await _app.ObtenerFacturasCompraBaseAsync("SN1")).Dato.ToList();

            Assert.Equal(new[] { "--", "DN" }, lista.Select(f => f.DocSubType));
        }

        [Fact]
        public async Task ObtenerFacturasCompraBase_MapeaElResumenConAcreditadoYDisponible()
        {
            _domain.Setup(d => d.ObtenerFacturasCompraBaseAsync("SN1")).ReturnsAsync(new List<FacturaCompraAcreditable>
            {
                new(new FacturaCompra { Entry = 12, NumDoc = 100, FechaDoc = new DateTime(2026, 10, 1), TotalDoc = 112m, MonedaDoc = "QTZ", CodigoSn = "SN1" }, 30m),
                new(new FacturaCompra { Entry = 13, NumDoc = 101, TotalDoc = 50m, MonedaDoc = "QTZ", CodigoSn = "SN1" }, 80m)   // acreditado > total: disponible 0
            });

            var respuesta = await _app.ObtenerFacturasCompraBaseAsync("SN1");

            var lista = respuesta.Dato.ToList();
            Assert.Equal(2, lista.Count);
            Assert.Equal(12, lista[0].Entry);
            Assert.Equal(100, lista[0].NumDoc);
            Assert.Equal(112m, lista[0].TotalDoc);
            Assert.Equal("QTZ", lista[0].MonedaDoc);
            Assert.Equal(30m, lista[0].TotalAcreditado);
            Assert.Equal(82m, lista[0].Disponible);
            Assert.Equal(80m, lista[1].TotalAcreditado);
            Assert.Equal(0m, lista[1].Disponible);
        }

        [Fact]
        public async Task ObtenerFacturaCompraBase_ConvierteLasLineasEnConceptos_YDevuelveElDisponible()
        {
            var factura = new FacturaCompra { Entry = 12, NumDoc = 100, CodigoSn = "SN1", NombreSn = "Proveedor", MonedaDoc = "QTZ", Direccion = "Zona 1", TotalDoc = 112m };
            var lineas = new List<FacturaCompraDetalle>
            {
                new() { Entry = 12, NoLinea = 1, CodArticulo = "ART1", Descripcion = "Tornillo", Cantidad = 3m, Precio = 5m, PrecioBruto = 15m, PrctjeDesc = 0m, CodigoImpuesto = "IVA", Impuesto = 1.8m, TotalLinea = 16.8m },
                new() { Entry = 12, NoLinea = 2, CodArticulo = "ART2", Descripcion = "  ", Cantidad = 1m, Precio = 10m, CodigoImpuesto = "IVA" }
            };
            _domain.Setup(d => d.ObtenerFacturaCompraBaseAsync(12)).ReturnsAsync(new FacturaCompraBaseCredito(factura, lineas, 12m));

            var respuesta = await _app.ObtenerFacturaCompraBaseAsync(12);

            Assert.True(respuesta.Resultado);
            var dato = respuesta.Dato;
            Assert.Equal((12, 100, "SN1", "Proveedor", "QTZ", "Zona 1"), (dato.Entry, dato.NumDoc, dato.CodigoSn, dato.NombreSn, dato.MonedaDoc, dato.Direccion));
            Assert.Equal(112m, dato.TotalDoc);
            Assert.Equal(12m, dato.TotalAcreditado);
            Assert.Equal(100m, dato.Disponible);
            Assert.Equal(2, dato.Lineas.Count);
            Assert.Equal("Tornillo", dato.Lineas[0].Descripcion);
            Assert.Equal(16.8m, dato.Lineas[0].TotalLinea);
            Assert.Equal("ART2", dato.Lineas[1].Descripcion);   // sin descripcion: usa el codigo del articulo
        }

        [Fact]
        public async Task ObtenerFacturaCompraBase_ErrorDelDominio_DevuelveElMensaje()
        {
            _domain.Setup(d => d.ObtenerFacturaCompraBaseAsync(777)).ThrowsAsync(new Exception("La factura de compra no existe."));

            var respuesta = await _app.ObtenerFacturaCompraBaseAsync(777);

            Assert.False(respuesta.Resultado);
            Assert.Equal("La factura de compra no existe.", respuesta.Mensaje);
        }
    }
}
