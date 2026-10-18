using API.Application.DTO.notaDebito;
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
    public class NotaDebitoApplicationTests
    {
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();
        private readonly Mock<INotaDebitoDomain> _domain = new();
        private readonly NotaDebitoApplication _app;

        public NotaDebitoApplicationTests()
        {
            _app = new NotaDebitoApplication(_domain.Object, _mapper);
        }

        [Fact]
        public async Task Insertar_MapeaEncabezadoYLineas_YDevuelveElEntry()
        {
            Factura? recibida = null;
            List<FacturaDetalle>? lineasRecibidas = null;
            _domain.Setup(d => d.InsertarAsync(It.IsAny<Factura>(), It.IsAny<IEnumerable<FacturaDetalle>>()))
                .Callback<Factura, IEnumerable<FacturaDetalle>>((n, l) => { recibida = n; lineasRecibidas = l.ToList(); })
                .ReturnsAsync(42);
            var dto = new NotaDebitoCrearDTO
            {
                Serie = 33, CodigoSn = "SN1", MonedaDoc = "QTZ", Comentario = "c",
                Lineas = { new NotaDebitoDetalleCrearDTO { Descripcion = "Flete", Cantidad = 2, Precio = 10m, CodigoImpuesto = "IVA" } }
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
            _domain.Setup(d => d.InsertarAsync(It.IsAny<Factura>(), It.IsAny<IEnumerable<FacturaDetalle>>()))
                .ThrowsAsync(new Exception("La serie no existe."));

            var respuesta = await _app.InsertarAsync(new NotaDebitoCrearDTO());

            Assert.False(respuesta.Resultado);
            Assert.Equal("La serie no existe.", respuesta.Mensaje);
        }

        [Fact]
        public async Task Actualizar_DominioDevuelveTrue_ResultadoTrue()
        {
            _domain.Setup(d => d.ActualizarAsync(5, It.IsAny<Factura>())).ReturnsAsync(true);

            var respuesta = await _app.ActualizarAsync(5, new NotaDebitoActualizarDTO { Cancelado = "S" });

            Assert.True(respuesta.Resultado);
            Assert.True(respuesta.Dato);
            Assert.Equal("Registro actualizado correctamente.", respuesta.Mensaje);
        }

        [Fact]
        public async Task Actualizar_DominioDevuelveFalse_ResultadoFalse()
        {
            _domain.Setup(d => d.ActualizarAsync(5, It.IsAny<Factura>())).ReturnsAsync(false);

            var respuesta = await _app.ActualizarAsync(5, new NotaDebitoActualizarDTO());

            Assert.False(respuesta.Resultado);
        }

        [Fact]
        public async Task Actualizar_ErrorDelDominio_DevuelveElMensaje()
        {
            _domain.Setup(d => d.ActualizarAsync(5, It.IsAny<Factura>()))
                .ThrowsAsync(new Exception("El documento está cancelado y no se puede modificar."));

            var respuesta = await _app.ActualizarAsync(5, new NotaDebitoActualizarDTO());

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
            _domain.Setup(d => d.ObtenerAsync(5)).ReturnsAsync(new Factura { Entry = 5, NumDoc = 9, CodigoSn = "SN1" });

            var respuesta = await _app.ObtenerAsync(5);

            Assert.True(respuesta.Resultado);
            Assert.Equal(9, respuesta.Dato.NumDoc);
        }

        [Fact]
        public async Task ObtenerTodo_MapeaTodos()
        {
            var lista = new List<Factura> { new() { Entry = 1 }, new() { Entry = 2 } };
            _domain.Setup(d => d.ObtenerTodoAsync()).ReturnsAsync(lista.AsAsyncQueryable());

            var respuesta = await _app.ObtenerTodoAsync();

            Assert.True(respuesta.Resultado);
            Assert.Equal(new[] { 1, 2 }, respuesta.Dato.Select(n => n.Entry));
        }

        [Fact]
        public async Task ObtenerLineas_MapeaLasLineas()
        {
            _domain.Setup(d => d.ObtenerLineasAsync(5)).ReturnsAsync(new List<FacturaDetalle>
            {
                new() { Entry = 5, NoLinea = 1, Descripcion = "A", CodigoImpuesto = "IVA" }
            });

            var respuesta = await _app.ObtenerLineasAsync(5);

            Assert.True(respuesta.Resultado);
            Assert.Equal("A", Assert.Single(respuesta.Dato).Descripcion);
        }

        [Fact]
        public async Task ObtenerFacturasBase_MapeaElResumen()
        {
            _domain.Setup(d => d.ObtenerFacturasBaseAsync("SN1")).ReturnsAsync(new List<Factura>
            {
                new() { Entry = 12, NumDoc = 100, FechaDoc = new DateTime(2026, 10, 1), TotalDoc = 112m, MonedaDoc = "QTZ", CodigoSn = "SN1" }
            });

            var respuesta = await _app.ObtenerFacturasBaseAsync("SN1");

            var f = Assert.Single(respuesta.Dato);
            Assert.Equal(12, f.Entry);
            Assert.Equal(100, f.NumDoc);
            Assert.Equal(112m, f.TotalDoc);
            Assert.Equal("QTZ", f.MonedaDoc);
        }

        [Fact]
        public async Task ObtenerFacturaBase_DevuelveLasLineasConArticuloYAlmacen()
        {
            var factura = new Factura { Entry = 12, NumDoc = 100, CodigoSn = "SN1", NombreSn = "Cliente", MonedaDoc = "QTZ", Direccion = "Zona 1" };
            var lineas = new List<FacturaDetalle>
            {
                new() { Entry = 12, NoLinea = 1, CodArticulo = "ART1", CodAlmacen = "01", Descripcion = "Tornillo", Cantidad = 3m, Precio = 5m, PrecioBruto = 15m, PrctjeDesc = 0m, CodigoImpuesto = "IVA", Impuesto = 1.8m, TotalLinea = 16.8m },
                new() { Entry = 12, NoLinea = 2, CodArticulo = "ART2", CodAlmacen = "02", Descripcion = "  ", Cantidad = 1m, Precio = 10m, CodigoImpuesto = "IVA" }
            };
            _domain.Setup(d => d.ObtenerFacturaBaseAsync(12)).ReturnsAsync(new FacturaBase(factura, lineas));

            var respuesta = await _app.ObtenerFacturaBaseAsync(12);

            Assert.True(respuesta.Resultado);
            var dato = respuesta.Dato;
            Assert.Equal((12, 100, "SN1", "Cliente", "QTZ", "Zona 1"), (dato.Entry, dato.NumDoc, dato.CodigoSn, dato.NombreSn, dato.MonedaDoc, dato.Direccion));
            Assert.Equal(2, dato.Lineas.Count);
            Assert.Equal(("ART1", "01", "Tornillo", 16.8m), (dato.Lineas[0].CodArticulo, dato.Lineas[0].CodAlmacen, dato.Lineas[0].Descripcion, dato.Lineas[0].TotalLinea));
            Assert.Equal(("ART2", "02", "ART2"), (dato.Lineas[1].CodArticulo, dato.Lineas[1].CodAlmacen, dato.Lineas[1].Descripcion));   // sin descripcion: usa el codigo del articulo
        }

        [Fact]
        public async Task ObtenerFacturaBase_ErrorDelDominio_DevuelveElMensaje()
        {
            _domain.Setup(d => d.ObtenerFacturaBaseAsync(777)).ThrowsAsync(new Exception("La factura no existe."));

            var respuesta = await _app.ObtenerFacturaBaseAsync(777);

            Assert.False(respuesta.Resultado);
            Assert.Equal("La factura no existe.", respuesta.Mensaje);
        }

        [Fact]
        public async Task Insertar_MapeaElModoYElArticuloDeLasLineas()
        {
            Factura? recibida = null;
            List<FacturaDetalle>? lineasRecibidas = null;
            _domain.Setup(d => d.InsertarAsync(It.IsAny<Factura>(), It.IsAny<IEnumerable<FacturaDetalle>>()))
                .Callback<Factura, IEnumerable<FacturaDetalle>>((n, l) => { recibida = n; lineasRecibidas = l.ToList(); })
                .ReturnsAsync(7);
            var dto = new NotaDebitoCrearDTO
            {
                Serie = 33, CodigoSn = "SN1", MonedaDoc = "QTZ", DocType = "I",
                Lineas = { new NotaDebitoDetalleCrearDTO { CodArticulo = "ART1", CodAlmacen = "01", Cantidad = 2, Precio = 10m, CodigoImpuesto = "IVA" } }
            };

            var respuesta = await _app.InsertarAsync(dto);

            Assert.True(respuesta.Resultado);
            Assert.Equal("I", recibida!.DocType);
            var linea = Assert.Single(lineasRecibidas!);
            Assert.Equal(("ART1", "01"), (linea.CodArticulo, linea.CodAlmacen));
        }
    }
}
