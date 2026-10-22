using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class MotocicletaDomainTests
    {
        private readonly Mock<IRepositorioGenerico<Motocicleta, int>> _repoMoto = new();
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocio = new();
        private readonly Mock<IRepositorioGenerico<OrdenServicio, int>> _repoOrden = new();
        private readonly List<Motocicleta> _motos = new();
        private readonly List<OrdenServicio> _ordenes = new();
        private readonly MotocicletaDomain _domain;

        public MotocicletaDomainTests()
        {
            _domain = new MotocicletaDomain(_repoMoto.Object, _repoSocio.Object, _repoOrden.Object);
            _repoMoto.Setup(r => r.InsertarAsync(It.IsAny<Motocicleta>())).ReturnsAsync((Motocicleta m) => { m.Entry = 7; return m; });
            _repoMoto.Setup(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<Motocicleta>())).ReturnsAsync(true);
            _repoMoto.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _motos.AsAsyncQueryable());
            _repoOrden.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _ordenes.AsAsyncQueryable());
            _repoSocio.Setup(r => r.ObtenerAsync("CL-1")).ReturnsAsync(new SocioNegocio { Codigo = "CL-1", TipoSn = "C", Activo = "S" });
        }

        private static Motocicleta Moto(Action<Motocicleta>? ajustar = null)
        {
            var m = new Motocicleta { CodigoSn = "CL-1", Placa = "m123abc", Marca = "Honda", Modelo = "CB125", Anio = 2022 };
            ajustar?.Invoke(m);
            return m;
        }

        private async Task<string> MensajeAlInsertar(Motocicleta m)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(m));
            return ex.Message;
        }

        [Fact]
        public async Task InsertarAsync_Valida_NormalizaPlacaYDevuelveEntry()
        {
            var entry = await _domain.InsertarAsync(Moto());

            Assert.Equal(7, entry);
            _repoMoto.Verify(r => r.InsertarAsync(It.Is<Motocicleta>(m => m.Placa == "M123ABC" && m.Activo == "S" && m.CodigoSn == "CL-1")), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_SinPlaca_Lanza()
        {
            Assert.Equal("La placa es requerida.", await MensajeAlInsertar(Moto(m => m.Placa = "  ")));
        }

        [Fact]
        public async Task InsertarAsync_SinCliente_Lanza()
        {
            Assert.Equal("El cliente de la motocicleta es requerido.", await MensajeAlInsertar(Moto(m => m.CodigoSn = "")));
        }

        [Fact]
        public async Task InsertarAsync_ClienteInexistente_Lanza()
        {
            _repoSocio.Setup(r => r.ObtenerAsync("CL-9")).ReturnsAsync((SocioNegocio?)null);

            Assert.Equal("El socio de negocio no existe.", await MensajeAlInsertar(Moto(m => m.CodigoSn = "CL-9")));
        }

        [Fact]
        public async Task InsertarAsync_SocioQueNoEsCliente_Lanza()
        {
            _repoSocio.Setup(r => r.ObtenerAsync("PR-1")).ReturnsAsync(new SocioNegocio { Codigo = "PR-1", TipoSn = "P", Activo = "S" });

            Assert.Equal("El socio de negocio debe ser un cliente.", await MensajeAlInsertar(Moto(m => m.CodigoSn = "PR-1")));
        }

        [Fact]
        public async Task InsertarAsync_ClienteInactivo_Lanza()
        {
            _repoSocio.Setup(r => r.ObtenerAsync("CL-2")).ReturnsAsync(new SocioNegocio { Codigo = "CL-2", TipoSn = "C", Activo = "N" });

            Assert.Equal("El socio de negocio está inactivo.", await MensajeAlInsertar(Moto(m => m.CodigoSn = "CL-2")));
        }

        [Fact]
        public async Task InsertarAsync_PlacaDuplicada_Lanza()
        {
            _motos.Add(new Motocicleta { Entry = 1, Placa = "M123ABC", CodigoSn = "CL-1", Activo = "S" });

            Assert.Equal("Ya existe una motocicleta con la placa: M123ABC", await MensajeAlInsertar(Moto()));
            _repoMoto.Verify(r => r.InsertarAsync(It.IsAny<Motocicleta>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_AnioInvalido_Lanza()
        {
            Assert.Equal("El año de la motocicleta no es válido.", await MensajeAlInsertar(Moto(m => m.Anio = 1800)));
        }

        [Fact]
        public async Task ActualizarAsync_Inexistente_DevuelveFalso()
        {
            _repoMoto.Setup(r => r.ObtenerAsync(99)).ReturnsAsync((Motocicleta?)null);

            Assert.False(await _domain.ActualizarAsync(99, Moto()));
        }

        [Fact]
        public async Task ActualizarAsync_CambiarDeClienteConOrdenes_Lanza()
        {
            _repoMoto.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new Motocicleta { Entry = 5, CodigoSn = "CL-1", Placa = "M123ABC", Activo = "S" });
            _repoSocio.Setup(r => r.ObtenerAsync("CL-3")).ReturnsAsync(new SocioNegocio { Codigo = "CL-3", TipoSn = "C", Activo = "S" });
            _ordenes.Add(new OrdenServicio { Entry = 1, MotoEntry = 5 });

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(5, Moto(m => m.CodigoSn = "CL-3")));

            Assert.Equal("La motocicleta tiene órdenes de servicio y no puede cambiar de cliente.", ex.Message);
            _repoMoto.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<Motocicleta>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_CambiarDeClienteSinOrdenes_Guarda()
        {
            _repoMoto.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new Motocicleta { Entry = 5, CodigoSn = "CL-1", Placa = "M123ABC", Activo = "S" });
            _repoSocio.Setup(r => r.ObtenerAsync("CL-3")).ReturnsAsync(new SocioNegocio { Codigo = "CL-3", TipoSn = "C", Activo = "S" });

            Assert.True(await _domain.ActualizarAsync(5, Moto(m => m.CodigoSn = "CL-3")));
            _repoMoto.Verify(r => r.ActualizarAsync(5, It.Is<Motocicleta>(m => m.CodigoSn == "CL-3")), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_MismoClienteConOrdenes_Guarda()
        {
            _repoMoto.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new Motocicleta { Entry = 5, CodigoSn = "CL-1", Placa = "M123ABC", Activo = "S" });
            _ordenes.Add(new OrdenServicio { Entry = 1, MotoEntry = 5 });

            Assert.True(await _domain.ActualizarAsync(5, Moto(m => m.Color = "Rojo")));
        }

        [Fact]
        public async Task ActualizarAsync_PlacaDeOtraMoto_Lanza()
        {
            _repoMoto.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new Motocicleta { Entry = 5, CodigoSn = "CL-1", Placa = "M111AAA", Activo = "S" });
            _motos.Add(new Motocicleta { Entry = 6, Placa = "M123ABC", CodigoSn = "CL-1", Activo = "S" });

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(5, Moto()));
            Assert.Equal("Ya existe una motocicleta con la placa: M123ABC", ex.Message);
        }

        [Fact]
        public async Task ActualizarAsync_ConsevaSuPropiaPlaca_Guarda()
        {
            _repoMoto.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new Motocicleta { Entry = 5, CodigoSn = "CL-1", Placa = "M123ABC", Activo = "S" });
            _motos.Add(new Motocicleta { Entry = 5, Placa = "M123ABC", CodigoSn = "CL-1", Activo = "S" });

            Assert.True(await _domain.ActualizarAsync(5, Moto()));
        }

        [Fact]
        public async Task EliminarAsync_HaceBajaLogicaConservandoLosDatos()
        {
            _repoMoto.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new Motocicleta { Entry = 5, CodigoSn = "CL-1", Placa = "M123ABC", Marca = "Honda", Activo = "S" });

            Assert.True(await _domain.EliminarAsync(5));
            _repoMoto.Verify(r => r.ActualizarAsync(5, It.Is<Motocicleta>(m => m.Activo == "N" && m.Placa == "M123ABC" && m.Marca == "Honda" && m.CodigoSn == "CL-1")), Times.Once);
            _repoMoto.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_Inexistente_DevuelveFalso()
        {
            _repoMoto.Setup(r => r.ObtenerAsync(99)).ReturnsAsync((Motocicleta?)null);

            Assert.False(await _domain.EliminarAsync(99));
        }

        [Fact]
        public async Task BuscarAsync_FiltraPorClienteYPlacaParcial()
        {
            _motos.Add(new Motocicleta { Entry = 1, CodigoSn = "CL-1", Placa = "M123ABC", Activo = "S" });
            _motos.Add(new Motocicleta { Entry = 2, CodigoSn = "CL-1", Placa = "M999ZZZ", Activo = "S" });
            _motos.Add(new Motocicleta { Entry = 3, CodigoSn = "CL-2", Placa = "M123XYZ", Activo = "S" });

            var porCliente = (await _domain.BuscarAsync("CL-1", null)).Select(m => m.Entry).ToList();
            var porPlaca = (await _domain.BuscarAsync(null, "m123")).Select(m => m.Entry).ToList();
            var ambos = (await _domain.BuscarAsync("CL-1", "m123")).Select(m => m.Entry).ToList();
            var todas = (await _domain.BuscarAsync(null, null)).Select(m => m.Entry).ToList();

            Assert.Equal(new[] { 1, 2 }, porCliente);
            Assert.Equal(new[] { 1, 3 }, porPlaca);
            Assert.Equal(new[] { 1 }, ambos);
            Assert.Equal(new[] { 1, 3, 2 }, todas); // ordenadas por placa: M123ABC, M123XYZ, M999ZZZ
        }

        [Fact]
        public async Task ObtenerHistorialAsync_DevuelveLasOrdenesDeLaMotoDeLaMasRecienteALaMasAntigua()
        {
            _ordenes.Add(new OrdenServicio { Entry = 1, MotoEntry = 5, FechaDoc = new DateTime(2026, 10, 1) });
            _ordenes.Add(new OrdenServicio { Entry = 2, MotoEntry = 9, FechaDoc = new DateTime(2026, 10, 2) });
            _ordenes.Add(new OrdenServicio { Entry = 3, MotoEntry = 5, FechaDoc = new DateTime(2026, 10, 3) });

            var historial = (await _domain.ObtenerHistorialAsync(5)).Select(o => o.Entry).ToList();

            Assert.Equal(new[] { 3, 1 }, historial);
        }
    }
}
