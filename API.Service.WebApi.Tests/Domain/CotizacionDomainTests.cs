using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class CotizacionDomainTests
    {
        private readonly Mock<IRepositorioGenerico<Cotizacion, int>> _repoCotizacionMock;
        private readonly Mock<IRepositorioGenerico<CotizacionDetalle, (int Entry, int NoLinea)>> _repoDetalleMock;
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracionMock;
        private readonly Mock<IDocumentoBaseService> _documentoBase = new();
        private readonly CotizacionDomain _domain;

        public CotizacionDomainTests()
        {
            _repoCotizacionMock = new Mock<IRepositorioGenerico<Cotizacion, int>>();
            _repoDetalleMock = new Mock<IRepositorioGenerico<CotizacionDetalle, (int Entry, int NoLinea)>>();
            _repoNumeracionMock = new Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>>();
            _domain = new CotizacionDomain(_repoCotizacionMock.Object, _repoDetalleMock.Object, _repoNumeracionMock.Object, _documentoBase.Object);
        }

        private static NumeracionDocumentoDet SerieAutogenerada(int? sigNumero = 5, int? finNumero = null, string bloqueado = "N") => new()
        {
            CodigoObj = "3",
            Serie = 4,
            NombreSerie = "Primario",
            SigNumero = sigNumero,
            FinNumero = finNumero,
            Bloqueado = bloqueado,
            Manual = "N",
            SubTipoDoc = "--",
            TipoSerie = "N"
        };

        [Fact]
        public async Task InsertarAsync_SerieAutogenerada_AsignaSigNumeroYLoIncrementa()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _repoCotizacionMock.Setup(r => r.InsertarAsync(It.IsAny<Cotizacion>()))
                .ReturnsAsync((Cotizacion c) => { c.Entry = 99; return c; });

            var obj = new Cotizacion { Serie = 4, NumDoc = 0, TipoObjeto = "algo-que-el-cliente-mando" };
            var entry = await _domain.InsertarAsync(obj);

            Assert.Equal(99, entry);
            Assert.Equal(5, obj.NumDoc);
            Assert.Equal("3", obj.TipoObjeto);
            Assert.Equal(6, serie.SigNumero);
            // El incremento de la serie no se persiste con una llamada aparte -- queda en la misma
            // entidad rastreada para guardarse junto con el INSERT de la cotización.
            _repoNumeracionMock.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<NumeracionDocumentoDet>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieManual_RespetaNumDocDelCliente()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            serie.Manual = "S";
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _repoCotizacionMock.Setup(r => r.InsertarAsync(It.IsAny<Cotizacion>()))
                .ReturnsAsync((Cotizacion c) => { c.Entry = 1; return c; });

            var obj = new Cotizacion { Serie = 4, NumDoc = 12345 };
            await _domain.InsertarAsync(obj);

            Assert.Equal(12345, obj.NumDoc);
            Assert.Equal(5, serie.SigNumero); // sin cambios: el consecutivo automático no aplica
        }

        [Fact]
        public async Task InsertarAsync_SerieManualSinNumDoc_Lanza()
        {
            var serie = SerieAutogenerada();
            serie.Manual = "S";
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var obj = new Cotizacion { Serie = 4, NumDoc = 0 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoCotizacionMock.Verify(r => r.InsertarAsync(It.IsAny<Cotizacion>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieBloqueada_Lanza()
        {
            var serie = SerieAutogenerada(bloqueado: "S");
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var obj = new Cotizacion { Serie = 4 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoCotizacionMock.Verify(r => r.InsertarAsync(It.IsAny<Cotizacion>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieAgotada_Lanza()
        {
            var serie = SerieAutogenerada(sigNumero: 10, finNumero: 9);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var obj = new Cotizacion { Serie = 4 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoCotizacionMock.Verify(r => r.InsertarAsync(It.IsAny<Cotizacion>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieInexistente_Lanza()
        {
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync((NumeracionDocumentoDet?)null);

            var obj = new Cotizacion { Serie = 4 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoCotizacionMock.Verify(r => r.InsertarAsync(It.IsAny<Cotizacion>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_FuerzaTipoObjetoATres()
        {
            _repoCotizacionMock.Setup(r => r.ActualizarAsync(1, It.IsAny<Cotizacion>())).ReturnsAsync(true);

            var obj = new Cotizacion { TipoObjeto = "otro-valor" };
            var resultado = await _domain.ActualizarAsync(1, obj);

            Assert.True(resultado);
            Assert.Equal("3", obj.TipoObjeto);
        }

        [Fact]
        public async Task EliminarAsync_ConDerivados_Lanza_YNoBorraNada()
        {
            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(TipoDocumentoVenta.Cotizacion, 1, "eliminar el documento"))
                .ThrowsAsync(new Exception("No se puede eliminar el documento: lo usan Pedido 3. Cancele primero esos documentos."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(1));

            Assert.Equal("No se puede eliminar el documento: lo usan Pedido 3. Cancele primero esos documentos.", ex.Message);
            _repoDetalleMock.Verify(r => r.EliminarAsync(It.IsAny<(int, int)>()), Times.Never);
            _repoCotizacionMock.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_PasarAEstadoCancelado_ValidaElOrigen()
        {
            _repoCotizacionMock.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new Cotizacion { Entry = 1, EstadoDoc = "A", CodigoSn = "CL-1" });
            _repoCotizacionMock.Setup(r => r.ActualizarAsync(1, It.IsAny<Cotizacion>())).ReturnsAsync(true);
            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(TipoDocumentoVenta.Cotizacion, 1, "cancelar el documento"))
                .ThrowsAsync(new Exception("bloqueado"));

            await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(1, new Cotizacion { EstadoDoc = "C", CodigoSn = "CL-1" }));

            _repoCotizacionMock.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<Cotizacion>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_CambiarElSocio_ValidaElOrigen()
        {
            _repoCotizacionMock.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new Cotizacion { Entry = 1, EstadoDoc = "A", CodigoSn = "CL-1" });
            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(TipoDocumentoVenta.Cotizacion, 1, "cambiar el socio de negocio"))
                .ThrowsAsync(new Exception("bloqueado"));

            await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(1, new Cotizacion { EstadoDoc = "A", CodigoSn = "CL-2" }));

            _repoCotizacionMock.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<Cotizacion>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_SinCancelarNiCambiarSocio_NoValida()
        {
            _repoCotizacionMock.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new Cotizacion { Entry = 1, EstadoDoc = "A", CodigoSn = "CL-1" });
            _repoCotizacionMock.Setup(r => r.ActualizarAsync(1, It.IsAny<Cotizacion>())).ReturnsAsync(true);

            var ok = await _domain.ActualizarAsync(1, new Cotizacion { EstadoDoc = "A", CodigoSn = "cl-1", Comentario = "x" });

            Assert.True(ok);
            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_YaCancelada_NoRevalida()
        {
            _repoCotizacionMock.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new Cotizacion { Entry = 1, EstadoDoc = "C", CodigoSn = "CL-1" });
            _repoCotizacionMock.Setup(r => r.ActualizarAsync(1, It.IsAny<Cotizacion>())).ReturnsAsync(true);

            await _domain.ActualizarAsync(1, new Cotizacion { EstadoDoc = "C", CodigoSn = "CL-1" });

            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }
    }
}
