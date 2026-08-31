using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class InventarioAsientoServiceTests
    {
        private readonly Mock<IRepositorioGenerico<Articulo, string>> _repoArt = new();
        private readonly Mock<IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)>> _repoExist = new();
        private readonly Mock<IRepositorioGenerico<MovimientoInventario, int>> _repoMov = new();
        private readonly InventarioAsientoService _svc;

        private readonly List<MovimientoInventario> _movAgregados = new();

        public InventarioAsientoServiceTests()
        {
            _svc = new InventarioAsientoService(_repoArt.Object, _repoExist.Object, _repoMov.Object, new ValuacionInventario());
            _repoMov.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<MovimientoInventario>()))
                .Callback<MovimientoInventario>(m => _movAgregados.Add(m))
                .Returns(Task.CompletedTask);
            _repoExist.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<ExistenciaArticulo>())).Returns(Task.CompletedTask);
        }

        private void ArticuloDeInventario(string cod, string metodo = "P", decimal costoProm = 0m, decimal costoEst = 0m, decimal cantActual = 0m) =>
            _repoArt.Setup(r => r.ObtenerAsync(cod)).ReturnsAsync(new Articulo
            {
                Codigo = cod, ArticuloInventario = "S", MetodoValuacion = metodo,
                CostoPromedio = costoProm, CostoEstandar = costoEst, CantDisponible = cantActual
            });

        private void SinExistenciaPrevia() =>
            _repoExist.Setup(r => r.ObtenerAsync(It.IsAny<(string, string)>())).ReturnsAsync((ExistenciaArticulo?)null);

        private void ConExistencia(string art, string alm, decimal disponible)
        {
            var key = (art, alm);
            _repoExist.Setup(r => r.ObtenerAsync(key)).ReturnsAsync(new ExistenciaArticulo
            {
                CodArticulo = art, CodAlmacen = alm, Disponible = disponible
            });
        }

        private static MovimientoRequest Req(string art, string alm, decimal cant, decimal precio) =>
            new("11", 100, 1, art, alm, cant, precio, new DateTime(2026, 8, 30));

        [Fact]
        public async Task AsentarAsync_PrimeraEntrada_CreaExistenciaYKardexConSaldos()
        {
            ArticuloDeInventario("ART1");
            SinExistenciaPrevia();

            await _svc.AsentarAsync(new[] { Req("ART1", "01", 10m, 25m) });

            _repoExist.Verify(r => r.AgregarSinGuardarAsync(It.Is<ExistenciaArticulo>(e =>
                e.CodArticulo == "ART1" && e.CodAlmacen == "01" && e.Disponible == 10m)), Times.Once);
            var mov = Assert.Single(_movAgregados);
            Assert.Equal(10m, mov.CantidadEntra);
            Assert.Equal(0m, mov.CantidadSale);
            Assert.Equal(25m, mov.CostoUnitario);
            Assert.Equal(250m, mov.ValorMovimiento);
            Assert.Equal(10m, mov.SaldoCantidad);
            Assert.Equal(25m, mov.SaldoCostoPromedio);
            Assert.Equal(250m, mov.SaldoValor);
            Assert.Null(mov.MovReversaDe);
            _repoMov.Verify(r => r.InsertarAsync(It.IsAny<MovimientoInventario>()), Times.Never);
        }

        [Fact]
        public async Task AsentarAsync_SegundaEntrada_AcumulaPromedioYExistencia()
        {
            ArticuloDeInventario("ART1", costoProm: 25m, cantActual: 10m);
            ConExistencia("ART1", "01", 10m);

            await _svc.AsentarAsync(new[] { Req("ART1", "01", 5m, 30m) });

            var mov = Assert.Single(_movAgregados);
            Assert.Equal(400m / 15m, mov.SaldoCostoPromedio);
            Assert.Equal(15m, mov.SaldoCantidad);
        }

        [Fact]
        public async Task AsentarAsync_ArticuloNoInventario_SeIgnora()
        {
            _repoArt.Setup(r => r.ObtenerAsync("SERV1")).ReturnsAsync(new Articulo { Codigo = "SERV1", ArticuloInventario = "N" });

            await _svc.AsentarAsync(new[] { Req("SERV1", "01", 3m, 10m) });

            Assert.Empty(_movAgregados);
            _repoExist.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<ExistenciaArticulo>()), Times.Never);
        }

        [Fact]
        public async Task AsentarAsync_SalidaQueDejaNegativo_Lanza()
        {
            ArticuloDeInventario("ART1", costoProm: 25m, cantActual: 2m);
            ConExistencia("ART1", "01", 2m);

            await Assert.ThrowsAsync<Exception>(() => _svc.AsentarAsync(new[] { Req("ART1", "01", -5m, 0m) }));
            Assert.Empty(_movAgregados);
        }

        [Fact]
        public async Task AsentarAsync_SalidaNegativaConPermitir_NoLanza()
        {
            ArticuloDeInventario("ART1", costoProm: 25m, cantActual: 2m);
            ConExistencia("ART1", "01", 2m);

            await _svc.AsentarAsync(new[] { Req("ART1", "01", -5m, 0m) }, permitirNegativo: true);

            var mov = Assert.Single(_movAgregados);
            Assert.Equal(5m, mov.CantidadSale);
            Assert.Equal(-3m, mov.SaldoCantidad);
        }

        [Fact]
        public async Task RevertirAsync_GeneraInversosYNoDuplica()
        {
            // Kardex del documento ("11", 100): una entrada de 10, sin reversa previa.
            var original = new MovimientoInventario
            {
                Entry = 500, TipoDoc = "11", DocEntry = 100, DocLinea = 1,
                CodArticulo = "ART1", CodAlmacen = "01", Fecha = new DateTime(2026, 8, 30),
                CantidadEntra = 10m, CantidadSale = 0m, CostoUnitario = 25m, MovReversaDe = null
            };
            _repoMov.Setup(r => r.ObtenerTodoAsync())
                .ReturnsAsync(new[] { original }.AsAsyncQueryable());
            ArticuloDeInventario("ART1", costoProm: 25m, cantActual: 10m);
            ConExistencia("ART1", "01", 10m);

            await _svc.RevertirAsync("11", 100);

            var rev = Assert.Single(_movAgregados);
            Assert.Equal(0m, rev.CantidadEntra);
            Assert.Equal(10m, rev.CantidadSale);
            Assert.Equal(500, rev.MovReversaDe);
            Assert.Equal(0m, rev.SaldoCantidad);
        }

        [Fact]
        public async Task RevertirAsync_YaRevertido_NoGeneraNada()
        {
            var original = new MovimientoInventario { Entry = 500, TipoDoc = "11", DocEntry = 100, DocLinea = 1, CodArticulo = "ART1", CodAlmacen = "01", CantidadEntra = 10m, MovReversaDe = null };
            var reversa  = new MovimientoInventario { Entry = 501, TipoDoc = "11", DocEntry = 100, DocLinea = 1, CodArticulo = "ART1", CodAlmacen = "01", CantidadSale = 10m, MovReversaDe = 500 };
            _repoMov.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new[] { original, reversa }.AsAsyncQueryable());

            await _svc.RevertirAsync("11", 100);

            Assert.Empty(_movAgregados);
        }
    }
}
