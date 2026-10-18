using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class NumeracionDocumentoDomainTests
    {
        private readonly Mock<IRepositorioGenerico<NumeracionDocumento, string>> _repo = new();
        private readonly NumeracionDocumentoDomain _domain;

        public NumeracionDocumentoDomainTests()
        {
            _domain = new NumeracionDocumentoDomain(_repo.Object);
            var filas = new List<NumeracionDocumento>
            {
                new() { CodigoObj = "6", SubTipoDoc = "--", DocAlias = "Factura", SerieDfct = 11 },
                new() { CodigoObj = "6", SubTipoDoc = "ND", DocAlias = "Nota Debito Cliente", SerieDfct = 33 },
                new() { CodigoObj = "1", SubTipoDoc = "C", DocAlias = "Socio de negocio", SerieDfct = 1 },
                new() { CodigoObj = "1", SubTipoDoc = "P", DocAlias = "Socio de negocio", SerieDfct = 2 }
            };
            _repo.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(filas.AsAsyncQueryable());
        }

        [Fact]
        public async Task ObtenerPorCodigo_SoloConCodigo_DevuelveLaPrimeraFilaDelObjeto()
        {
            var fila = await _domain.ObtenerPorCodigoAsync("6");

            Assert.Equal("--", fila.SubTipoDoc);
        }

        [Fact]
        public async Task ObtenerPorCodigo_SoloConCodigo_PrefiereLaFilaPorDefectoAunqueNoSeaLaPrimera()
        {
            var filas = new List<NumeracionDocumento>
            {
                new() { CodigoObj = "6", SubTipoDoc = "ND", DocAlias = "Nota Debito Cliente", SerieDfct = 33 },
                new() { CodigoObj = "6", SubTipoDoc = "--", DocAlias = "Factura", SerieDfct = 11 }
            };
            _repo.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(filas.AsAsyncQueryable());

            var fila = await _domain.ObtenerPorCodigoAsync("6");

            Assert.Equal("--", fila.SubTipoDoc);
            Assert.Equal("Factura", fila.DocAlias);
        }

        [Fact]
        public async Task ObtenerPorCodigo_SoloConCodigoSinFilaPorDefecto_DevuelveLaPrimeraFila()
        {
            var fila = await _domain.ObtenerPorCodigoAsync("1");

            Assert.Equal("C", fila.SubTipoDoc);
        }

        [Theory]
        [InlineData("6:ND", "Nota Debito Cliente")]
        [InlineData("6:--", "Factura")]
        [InlineData("1:P", "Socio de negocio")]
        public async Task ObtenerPorCodigo_ConSubtipo_DevuelveLaFilaExacta(string clave, string alias)
        {
            var fila = await _domain.ObtenerPorCodigoAsync(clave);

            Assert.Equal(alias, fila.DocAlias);
            Assert.Equal(clave.Split(':')[1], fila.SubTipoDoc);
        }

        [Fact]
        public async Task ObtenerPorCodigo_ConSubtipoInexistente_DevuelveNull()
        {
            Assert.Null(await _domain.ObtenerPorCodigoAsync("6:XX"));
        }

        [Fact]
        public async Task Insertar_MismoObjetoConOtroSubtipo_Permite()
        {
            var nueva = new NumeracionDocumento { CodigoObj = "6", SubTipoDoc = "XX", DocAlias = "Otro subtipo" };
            _repo.Setup(r => r.InsertarAsync(nueva)).ReturnsAsync(nueva);

            Assert.True(await _domain.InsertarAsync(nueva));

            _repo.Verify(r => r.InsertarAsync(nueva), Times.Once);
        }

        [Fact]
        public async Task Insertar_ObjetoYSubtipoRepetidos_LanzaConElSubtipo()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(new NumeracionDocumento { CodigoObj = "6", SubTipoDoc = "ND" }));

            Assert.Equal("Ya existe un registro con el código: 6 (ND)", ex.Message);
            _repo.Verify(r => r.InsertarAsync(It.IsAny<NumeracionDocumento>()), Times.Never);
        }

        [Fact]
        public async Task Insertar_ObjetoRepetidoConSubtipoPorDefecto_LanzaSinSubtipo()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(new NumeracionDocumento { CodigoObj = "6", SubTipoDoc = "--" }));

            Assert.Equal("Ya existe un registro con el código: 6", ex.Message);
        }

        [Fact]
        public async Task ActualizarYEliminar_PasanLaClaveCompletaAlRepositorio()
        {
            var datos = new NumeracionDocumento { SerieDfct = 34 };
            _repo.Setup(r => r.ActualizarAsync("6:ND", datos)).ReturnsAsync(true);
            _repo.Setup(r => r.EliminarAsync("6:ND")).ReturnsAsync(true);

            Assert.True(await _domain.ActualizarAsync("6:ND", datos));
            Assert.True(await _domain.EliminarAsync("6:ND"));
        }
    }
}
