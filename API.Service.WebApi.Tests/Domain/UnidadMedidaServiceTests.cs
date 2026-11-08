using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class UnidadMedidaServiceTests
    {
        private const int Unidad = 1, Caja = 2, Docena = 3, Libra = 4, Kilo = 5;

        private readonly Mock<IRepositorioGenerico<Articulo, string>> _repoArticulo = new();
        private readonly Mock<IRepositorioGenerico<GrupoUnidadMedidaArticulo, int>> _repoGrupo = new();
        private readonly Mock<IRepositorioGenerico<GrupoUnidadMedidaDetArticulo, (int GrpMedidaEntry, int NumLinea)>> _repoDet = new();
        private readonly Mock<IRepositorioGenerico<UnidadMedidaArticulo, int>> _repoUnidad = new();
        private readonly UnidadMedidaService _servicio;

        private readonly List<GrupoUnidadMedidaDetArticulo> _detalles = new();

        public UnidadMedidaServiceTests()
        {
            _servicio = new UnidadMedidaService(_repoArticulo.Object, _repoGrupo.Object, _repoDet.Object, _repoUnidad.Object);

            _repoDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _detalles.AsAsyncQueryable());
            _repoUnidad.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => new List<UnidadMedidaArticulo>
            {
                new() { Entry = Unidad, Codigo = "UND", Nombre = "Unidad" },
                new() { Entry = Caja, Codigo = "CAJA", Nombre = "Caja de 12" },
                new() { Entry = Docena, Codigo = "DOC", Nombre = "Docena" },
                new() { Entry = Libra, Codigo = "LB", Nombre = "Libra" },
                new() { Entry = Kilo, Codigo = "KG", Nombre = "Kilogramo" },
            }.AsAsyncQueryable());
        }

        private void Articulo(string codigo, int? grupo) =>
            _repoArticulo.Setup(r => r.ObtenerAsync(codigo)).ReturnsAsync(new Articulo { Codigo = codigo, CodigoGrpUnidadMedida = grupo });

        private void Grupo(int entry, int baseMedida) =>
            _repoGrupo.Setup(r => r.ObtenerAsync(entry)).ReturnsAsync(new GrupoUnidadMedidaArticulo { Entry = entry, BaseMedida = baseMedida });

        private void Fila(int grupo, int medida, decimal alternativa, decimal cantBase, string? activo = "S") =>
            _detalles.Add(new GrupoUnidadMedidaDetArticulo
            {
                GrpMedidaEntry = grupo, MedidaEntry = medida, CantAlternativa = alternativa, CantBase = cantBase,
                NumLinea = _detalles.Count(d => d.GrpMedidaEntry == grupo) + 1, Activo = activo
            });

        private void GrupoCajas()
        {
            Articulo("A1", 10);
            Grupo(10, Unidad);
            Fila(10, Unidad, 1, 1);
            Fila(10, Caja, 1, 12);
            Fila(10, Docena, 1, 12);
        }

        [Fact]
        public async Task SinUnidadElegida_UsaLaBaseDelGrupo()
        {
            GrupoCajas();
            var r = await _servicio.ResolverAsync("A1", null);
            Assert.Equal(Unidad, r.UomEntry);
            Assert.Equal("UND", r.Codigo);
            Assert.Equal(1m, r.Factor);
        }

        [Fact]
        public async Task UnidadAlternativa_DevuelveSuFactor()
        {
            GrupoCajas();
            var r = await _servicio.ResolverAsync("A1", Caja);
            Assert.Equal(Caja, r.UomEntry);
            Assert.Equal(12m, r.Factor);
        }

        [Fact]
        public async Task UnidadBaseElegidaExplicitamente_TieneFactorUno()
        {
            GrupoCajas();
            var r = await _servicio.ResolverAsync("A1", Unidad);
            Assert.Equal(1m, r.Factor);
        }

        [Fact]
        public async Task FactorFraccionario_SeCalculaComoCantBaseEntreCantAlternativa()
        {
            Articulo("A2", 20);
            Grupo(20, Kilo);
            Fila(20, Kilo, 1, 1);
            Fila(20, Libra, 1, 0.4536m);
            var r = await _servicio.ResolverAsync("A2", Libra);
            Assert.Equal(0.4536m, r.Factor);

            _detalles.Add(new GrupoUnidadMedidaDetArticulo { GrpMedidaEntry = 20, MedidaEntry = Docena, CantAlternativa = 4, CantBase = 1, NumLinea = 3, Activo = "S" });
            Assert.Equal(0.25m, (await _servicio.ResolverAsync("A2", Docena)).Factor);
        }

        [Fact]
        public async Task GrupoSinFilaParaLaBase_AunAsiAceptaLaBase()
        {
            Articulo("A3", 30);
            Grupo(30, Unidad);
            Fila(30, Caja, 1, 6);
            var r = await _servicio.ResolverAsync("A3", null);
            Assert.Equal(Unidad, r.UomEntry);
            Assert.Equal(1m, r.Factor);
        }

        [Fact]
        public async Task UnidadAjenaAlGrupo_SeRechaza()
        {
            GrupoCajas();
            var ex = await Assert.ThrowsAsync<Exception>(() => _servicio.ResolverAsync("A1", Libra));
            Assert.Equal($"La unidad de medida {Libra} no pertenece al grupo de medida del artículo A1.", ex.Message);
        }

        [Fact]
        public async Task UnidadInactivaEnElGrupo_SeRechaza()
        {
            Articulo("A1", 10);
            Grupo(10, Unidad);
            Fila(10, Unidad, 1, 1);
            Fila(10, Caja, 1, 12, activo: "N");
            await Assert.ThrowsAsync<Exception>(() => _servicio.ResolverAsync("A1", Caja));
        }

        [Fact]
        public async Task FilaActivaNula_SeConsideraActiva()
        {
            Articulo("A1", 10);
            Grupo(10, Unidad);
            Fila(10, Caja, 1, 12, activo: null);
            Assert.Equal(12m, (await _servicio.ResolverAsync("A1", Caja)).Factor);
        }

        [Fact]
        public async Task EquivalenciaInvalida_SeRechaza()
        {
            Articulo("A1", 10);
            Grupo(10, Unidad);
            Fila(10, Caja, 0, 12);
            await Assert.ThrowsAsync<Exception>(() => _servicio.ResolverAsync("A1", null));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(-1)]
        public async Task ArticuloSinGrupoOGrupoManual_SinUnidadYFactorUno(int? grupo)
        {
            Articulo("A4", grupo);
            var r = await _servicio.ResolverAsync("A4", null);
            Assert.Null(r.UomEntry);
            Assert.Null(r.Codigo);
            Assert.Equal(1m, r.Factor);
        }

        [Fact]
        public async Task ArticuloSinGrupo_ConUnidadExplicita_SeRechaza()
        {
            Articulo("A4", null);
            await Assert.ThrowsAsync<Exception>(() => _servicio.ResolverAsync("A4", Caja));
        }

        [Fact]
        public async Task ArticuloInexistente_SeRechaza()
        {
            _repoArticulo.Setup(r => r.ObtenerAsync("X")).ReturnsAsync((Articulo?)null);
            var ex = await Assert.ThrowsAsync<API.Domain.Core.Inventario.ArticuloNoExisteException>(() => _servicio.ResolverAsync("X", null));
            Assert.Equal("El artículo X no existe.", ex.Message);
        }

        [Fact]
        public async Task ObtenerUnidades_ListaLaBasePrimeroYLasAlternativasSinRepetir()
        {
            GrupoCajas();
            var r = await _servicio.ObtenerUnidadesAsync("A1");
            Assert.Equal(10, r.GrupoEntry);
            Assert.Equal(new[] { Unidad, Caja, Docena }, r.Unidades.Select(u => u.UomEntry));
            Assert.True(r.Unidades[0].EsBase);
            Assert.All(r.Unidades.Skip(1), u => Assert.False(u.EsBase));
            Assert.Equal(new[] { 1m, 12m, 12m }, r.Unidades.Select(u => u.Factor));
        }

        [Fact]
        public async Task ObtenerUnidades_ArticuloSinGrupo_ListaVacia()
        {
            Articulo("A4", null);
            var r = await _servicio.ObtenerUnidadesAsync("A4");
            Assert.Null(r.GrupoEntry);
            Assert.Empty(r.Unidades);
        }
    }
}
