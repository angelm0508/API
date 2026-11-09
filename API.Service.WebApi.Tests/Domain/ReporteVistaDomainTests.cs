using API.Domain.Core.Reportes;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class ReporteVistaDomainTests
    {
        private readonly Mock<IRepositorioGenerico<ReporteVista, int>> _repo = new();
        private readonly List<ReporteVista> _datos = new();
        private readonly ReporteVistaDomain _domain;

        public ReporteVistaDomainTests()
        {
            _repo.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _datos.AsAsyncQueryable());
            _repo.Setup(r => r.InsertarAsync(It.IsAny<ReporteVista>()))
                 .ReturnsAsync((ReporteVista v) => { v.Entry = _datos.Count + 100; _datos.Add(v); return v; });
            _repo.Setup(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<ReporteVista>())).ReturnsAsync(true);
            _repo.Setup(r => r.EliminarAsync(It.IsAny<int>())).ReturnsAsync(true);
            _domain = new ReporteVistaDomain(_repo.Object);
        }

        private static ReporteVista V(int entry, int usuario, string reporte, string nombre, string config = "{}") =>
            new() { Entry = entry, UsuarioId = usuario, CodigoReporte = reporte, Nombre = nombre, Configuracion = config, FechaCreacion = DateTime.Today, FechaActualizacion = DateTime.Today };

        private static async Task<string> MensajeAsync(Task tarea) =>
            (await Assert.ThrowsAsync<ReporteInvalidoException>(() => tarea)).Message;

        [Fact]
        public async Task Obtener_SoloLasVistasDelUsuarioYDelReporteOrdenadasPorNombre()
        {
            _datos.AddRange(new[] { V(1, 5, "r1", "Zeta"), V(2, 5, "r1", "Alfa"), V(3, 6, "r1", "Ajena"), V(4, 5, "r2", "Otro reporte") });

            var vistas = await _domain.ObtenerAsync(5, "r1");

            Assert.Equal(new[] { "Alfa", "Zeta" }, vistas.Select(v => v.Nombre).ToArray());
        }

        [Fact]
        public async Task Guardar_Nueva_InsertaConElNombreRecortado()
        {
            var dato = await _domain.GuardarAsync(5, "r1", "  Mi vista  ", "{\"columnas\":[\"a\"]}");

            Assert.Equal("Mi vista", dato.Nombre);
            Assert.Equal("{\"columnas\":[\"a\"]}", dato.Configuracion);
            var guardada = Assert.Single(_datos);
            Assert.Equal(5, guardada.UsuarioId);
            Assert.Equal("r1", guardada.CodigoReporte);
            _repo.Verify(r => r.InsertarAsync(It.IsAny<ReporteVista>()), Times.Once);
        }

        [Fact]
        public async Task Guardar_ConUnNombreExistenteSinDistinguirMayusculas_LaReemplaza()
        {
            _datos.Add(V(1, 5, "r1", "Mi Vista", "{\"a\":1}"));

            var dato = await _domain.GuardarAsync(5, "r1", "mi vista", "{\"a\":2}");

            Assert.Equal(1, dato.Entry);
            Assert.Equal("{\"a\":2}", _datos[0].Configuracion);
            _repo.Verify(r => r.ActualizarAsync(1, It.IsAny<ReporteVista>()), Times.Once);
            _repo.Verify(r => r.InsertarAsync(It.IsAny<ReporteVista>()), Times.Never);
        }

        [Fact]
        public async Task Guardar_OtroUsuarioOReporteConElMismoNombre_CreaUnaVistaNueva()
        {
            _datos.Add(V(1, 6, "r1", "Mi vista"));
            _datos.Add(V(2, 5, "r2", "Mi vista"));

            await _domain.GuardarAsync(5, "r1", "Mi vista", "{}");

            Assert.Equal(3, _datos.Count);
        }

        [Theory]
        [InlineData("", "El nombre de la vista es obligatorio.")]
        [InlineData("   ", "El nombre de la vista es obligatorio.")]
        public async Task Guardar_SinNombre_Lanza(string nombre, string mensaje)
        {
            Assert.Equal(mensaje, await MensajeAsync(_domain.GuardarAsync(5, "r1", nombre, "{}")));
            _repo.Verify(r => r.InsertarAsync(It.IsAny<ReporteVista>()), Times.Never);
        }

        [Fact]
        public async Task Guardar_NombreMuyLargo_Lanza()
        {
            Assert.Equal("El nombre de la vista no puede superar 100 caracteres.",
                await MensajeAsync(_domain.GuardarAsync(5, "r1", new string('x', 101), "{}")));
        }

        [Theory]
        [InlineData("")]
        [InlineData("no es json")]
        [InlineData("[1,2]")]
        [InlineData("\"texto\"")]
        public async Task Guardar_ConfiguracionQueNoEsUnObjetoJson_Lanza(string config)
        {
            Assert.Equal("La configuración de la vista no es válida.", await MensajeAsync(_domain.GuardarAsync(5, "r1", "V", config)));
        }

        [Fact]
        public async Task Guardar_ConfiguracionMuyGrande_Lanza()
        {
            var grande = "{\"x\":\"" + new string('a', ReporteVistaDomain.MaxTamanoConfiguracion) + "\"}";

            Assert.Equal("La configuración de la vista es demasiado grande.", await MensajeAsync(_domain.GuardarAsync(5, "r1", "V", grande)));
        }

        [Fact]
        public async Task Guardar_ConCincuentaVistasDelUsuario_NoDejaCrearOtraPeroSiReemplazar()
        {
            for (var i = 0; i < ReporteVistaDomain.MaxVistasPorUsuario; i++) _datos.Add(V(i + 1, 5, i % 2 == 0 ? "r1" : "r2", "Vista " + i));

            Assert.Equal("Ya tienes 50 vistas guardadas. Borra alguna para crear otra.",
                await MensajeAsync(_domain.GuardarAsync(5, "r1", "Nueva", "{}")));

            var reemplazo = await _domain.GuardarAsync(5, "r1", "Vista 0", "{\"x\":1}");
            Assert.Equal(1, reemplazo.Entry);
        }

        [Fact]
        public async Task Guardar_ElLimiteEsPorUsuario_NoAfectaAOtros()
        {
            for (var i = 0; i < ReporteVistaDomain.MaxVistasPorUsuario; i++) _datos.Add(V(i + 1, 6, "r1", "Vista " + i));

            var dato = await _domain.GuardarAsync(5, "r1", "Nueva", "{}");

            Assert.Equal("Nueva", dato.Nombre);
        }

        [Fact]
        public async Task Eliminar_VistaPropia_LaBorra()
        {
            _datos.Add(V(1, 5, "r1", "Mi vista"));

            Assert.True(await _domain.EliminarAsync(5, "r1", 1));
            _repo.Verify(r => r.EliminarAsync(1), Times.Once);
        }

        [Fact]
        public async Task Eliminar_VistaAjenaOInexistenteOdeOtroReporte_DevuelveFalseYNoBorra()
        {
            _datos.Add(V(1, 6, "r1", "Ajena"));
            _datos.Add(V(2, 5, "r2", "Otro reporte"));

            Assert.False(await _domain.EliminarAsync(5, "r1", 1));
            Assert.False(await _domain.EliminarAsync(5, "r1", 2));
            Assert.False(await _domain.EliminarAsync(5, "r1", 99));
            _repo.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
        }
    }
}
