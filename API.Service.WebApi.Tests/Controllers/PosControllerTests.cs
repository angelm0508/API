using System.Reflection;
using System.Security.Claims;
using API.Application.DTO;
using API.Application.Interface;
using API.Domain.Interface;
using API.Domain.Interface.Pos;
using API.Service.WebApi.Authorization;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class PosControllerTests
    {
        private readonly Mock<IPosApplication> _app = new();

        private PosController Crear(ClaimsPrincipal? usuario = null) =>
            new(_app.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = usuario ?? ReporteControllerTests.Usuario() } }
            };

        private static readonly PosConfiguracionDato Dato = new(1, 2, "ALM1", "IVA", "C1", "EFE", "GTQ");

        private static Respuesta<PosConfiguracionVista> Buena() => new()
        {
            Resultado = true,
            Mensaje = "ok",
            Dato = new PosConfiguracionVista(Dato, Array.Empty<string>())
        };

        // ---------- GET Configuracion ----------

        [Theory]
        [InlineData("Pos:V")]
        [InlineData("PosConfiguracion:V")]
        public async Task ObtenerConfiguracion_ConPermisoDeVerDeCualquierModulo_Devuelve200(string perm)
        {
            var buena = Buena();
            _app.Setup(a => a.ObtenerConfiguracionAsync()).ReturnsAsync(buena);

            var r = await Crear(ReporteControllerTests.Usuario(perm)).ObtenerConfiguracion();

            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task ObtenerConfiguracion_ElSuperusuario_Devuelve200()
        {
            _app.Setup(a => a.ObtenerConfiguracionAsync()).ReturnsAsync(Buena());

            var r = await Crear(ReporteControllerTests.Usuario(superusuario: true)).ObtenerConfiguracion();

            Assert.IsType<OkObjectResult>(r.Result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("Pos:A")]
        [InlineData("Facturas:V")]
        public async Task ObtenerConfiguracion_SinNingunPermisoDeVer_Devuelve403SinTocarLaAplicacion(string? perm)
        {
            var r = await Crear(ReporteControllerTests.Usuario(perm)).ObtenerConfiguracion();

            var o = Assert.IsType<ObjectResult>(r.Result);
            Assert.Equal(403, o.StatusCode);
            Assert.Equal(PermisoUsuario.MensajeDenegado, ((Respuesta<object>)o.Value!).Mensaje);
            _app.Verify(a => a.ObtenerConfiguracionAsync(), Times.Never);
        }

        [Fact]
        public async Task ObtenerConfiguracion_ConError_Devuelve400()
        {
            _app.Setup(a => a.ObtenerConfiguracionAsync()).ReturnsAsync(new Respuesta<PosConfiguracionVista> { Mensaje = "fallo" });

            var r = await Crear(ReporteControllerTests.Usuario("Pos:V")).ObtenerConfiguracion();

            Assert.IsType<BadRequestObjectResult>(r.Result);
        }

        // ---------- PUT Configuracion ----------

        [Fact]
        public async Task GuardarConfiguracion_Exito_Devuelve200()
        {
            var buena = Buena();
            _app.Setup(a => a.GuardarConfiguracionAsync(Dato)).ReturnsAsync(buena);

            var r = await Crear().GuardarConfiguracion(Dato);

            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task GuardarConfiguracion_ConError_Devuelve400()
        {
            _app.Setup(a => a.GuardarConfiguracionAsync(Dato))
                .ReturnsAsync(new Respuesta<PosConfiguracionVista> { Mensaje = "El almacén no existe." });

            var r = await Crear().GuardarConfiguracion(Dato);

            Assert.IsType<BadRequestObjectResult>(r.Result);
        }

        [Fact]
        public async Task GuardarConfiguracion_CuerpoNulo_Devuelve400SinTocarLaAplicacion()
        {
            var r = await Crear().GuardarConfiguracion(null);

            Assert.IsType<BadRequestObjectResult>(r.Result);
            _app.Verify(a => a.GuardarConfiguracionAsync(It.IsAny<PosConfiguracionDato>()), Times.Never);
        }

        // ---------- forma del controlador ----------

        [Fact]
        public void ElControllerExigeAutenticacionYCuelgaDeApiPos()
        {
            Assert.NotNull(typeof(PosController).GetCustomAttribute<AuthorizeAttribute>());
            Assert.NotNull(typeof(PosController).GetCustomAttribute<ApiControllerAttribute>());
            Assert.Equal("api/Pos", typeof(PosController).GetCustomAttribute<RouteAttribute>()!.Template);
        }

        [Fact]
        public void Put_LlevaElPermisoActualizarDePosConfiguracion()
        {
            var m = typeof(PosController).GetMethod(nameof(PosController.GuardarConfiguracion))!;
            Assert.Equal("Configuracion", m.GetCustomAttribute<HttpPutAttribute>()!.Template);
            var atributo = Assert.Single(m.GetCustomAttributes<RequierePermisoAttribute>());
            Assert.Equal(("PosConfiguracion", Accion.Actualizar), Permiso(atributo));
        }

        [Fact]
        public void Get_EvaluaElPermisoDentroDeLaAccion_SinAtributoDePermiso()
        {
            var m = typeof(PosController).GetMethod(nameof(PosController.ObtenerConfiguracion))!;
            Assert.Equal("Configuracion", m.GetCustomAttribute<HttpGetAttribute>()!.Template);
            Assert.Null(m.GetCustomAttribute<RequierePermisoAttribute>());
        }

        private static (string, Accion) Permiso(RequierePermisoAttribute a)
        {
            var t = typeof(RequierePermisoAttribute);
            var modulo = (string)t.GetField("_codigoModulo", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(a)!;
            var accion = (Accion)t.GetField("_accion", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(a)!;
            return (modulo, accion);
        }
    }
}
