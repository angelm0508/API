using System.Security.Claims;
using API.Application.DTO;
using API.Application.DTO.reporte;
using API.Application.Interface;
using API.Domain.Interface.Reportes;
using API.Service.WebApi.Authorization;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class ReporteControllerTests
    {
        private readonly Mock<IReporteApplication> _app = new();

        internal static ClaimsPrincipal Usuario(string? perm = null, bool superusuario = false, string id = "5")
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, id), new(ClaimTypes.Name, "tester") };
            if (superusuario) claims.Add(new Claim("SuperUsuario", "S"));
            if (perm is not null) claims.Add(new Claim("perm", perm));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        }

        private ReporteController Crear(ClaimsPrincipal? usuario = null) =>
            new(_app.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = usuario ?? Usuario() } }
            };

        private static Respuesta<ReporteDefinicion> DefinicionOk(string modulo = "ReportesVentas") => new()
        {
            Resultado = true,
            Dato = new ReporteDefinicion("ventas", "V", "d", "Ventas", modulo, Array.Empty<FiltroReporte>(), Array.Empty<ColumnaReporte>())
        };

        private static void Es403(IActionResult resultado)
        {
            var o = Assert.IsType<ObjectResult>(resultado);
            Assert.Equal(403, o.StatusCode);
            Assert.Equal(PermisoUsuario.MensajeDenegado, ((Respuesta<object>)o.Value!).Mensaje);
        }

        // ---------- catálogo ----------

        [Fact]
        public void Catalogo_FiltraPorElPermisoVerDeCadaModulo()
        {
            Func<string, bool>? predicado = null;
            var buena = new Respuesta<List<ReporteResumenDTO>> { Resultado = true, Dato = new() };
            _app.Setup(a => a.ObtenerCatalogo(It.IsAny<Func<string, bool>>()))
                .Callback<Func<string, bool>>(f => predicado = f)
                .Returns(buena);

            var r = Crear(Usuario("ReportesVentas:V")).Catalogo();

            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);
            Assert.True(predicado!("ReportesVentas"));
            Assert.False(predicado("ReportesCompras"));
        }

        [Fact]
        public void Catalogo_ElSuperusuarioVeTodo()
        {
            Func<string, bool>? predicado = null;
            _app.Setup(a => a.ObtenerCatalogo(It.IsAny<Func<string, bool>>()))
                .Callback<Func<string, bool>>(f => predicado = f)
                .Returns(new Respuesta<List<ReporteResumenDTO>> { Resultado = true, Dato = new() });

            Crear(Usuario(superusuario: true)).Catalogo();

            Assert.True(predicado!("ReportesCobrosPagos"));
        }

        [Fact]
        public void Catalogo_ConError_DevuelveBadRequest()
        {
            var mala = new Respuesta<List<ReporteResumenDTO>> { Resultado = false, Mensaje = "falla" };
            _app.Setup(a => a.ObtenerCatalogo(It.IsAny<Func<string, bool>>())).Returns(mala);

            var r = Crear().Catalogo();

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>(r.Result).Value);
        }

        // ---------- definición ----------

        [Fact]
        public void Definicion_ConPermiso_DevuelveOk()
        {
            var buena = DefinicionOk();
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(buena);

            var r = Crear(Usuario("ReportesVentas:V")).Definicion("ventas");

            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public void Definicion_SinPermisoDelModulo_Devuelve403()
        {
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());

            Es403(Crear(Usuario("ReportesCompras:V")).Definicion("ventas").Result!);
        }

        [Fact]
        public void Definicion_Inexistente_DevuelveNotFound()
        {
            var mala = new Respuesta<ReporteDefinicion> { Resultado = false, Mensaje = "El reporte 'x' no existe." };
            _app.Setup(a => a.ObtenerDefinicion("x")).Returns(mala);

            var r = Crear().Definicion("x");

            Assert.Same(mala, Assert.IsType<NotFoundObjectResult>(r.Result).Value);
        }

        // ---------- ejecutar ----------

        [Fact]
        public async Task Ejecutar_ConPermiso_EjecutaYDevuelveOk()
        {
            var solicitud = new SolicitudReporte();
            var buena = new Respuesta<ResultadoReporte> { Resultado = true, Dato = new ResultadoReporte() };
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());
            _app.Setup(a => a.EjecutarAsync("ventas", solicitud)).ReturnsAsync(buena);

            var r = await Crear(Usuario("ReportesVentas:V")).Ejecutar("ventas", solicitud);

            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task Ejecutar_SinPermiso_Devuelve403YNoEjecuta()
        {
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());

            var r = await Crear(Usuario("ReportesVentas:A")).Ejecutar("ventas", new SolicitudReporte());

            Es403(r.Result!);
            _app.Verify(a => a.EjecutarAsync(It.IsAny<string>(), It.IsAny<SolicitudReporte>()), Times.Never);
        }

        [Fact]
        public async Task Ejecutar_ReporteInexistente_DevuelveNotFound()
        {
            _app.Setup(a => a.ObtenerDefinicion("x")).Returns(new Respuesta<ReporteDefinicion> { Resultado = false, Mensaje = "El reporte 'x' no existe." });

            var r = await Crear().Ejecutar("x", new SolicitudReporte());

            Assert.IsType<NotFoundObjectResult>(r.Result);
        }

        [Fact]
        public async Task Ejecutar_SolicitudNula_DevuelveBadRequest()
        {
            var r = await Crear().Ejecutar("ventas", null!);

            var mala = Assert.IsType<BadRequestObjectResult>(r.Result);
            Assert.Equal("La solicitud del reporte es obligatoria.", ((Respuesta<ResultadoReporte>)mala.Value!).Mensaje);
        }

        [Fact]
        public async Task Ejecutar_ErrorDeNegocio_DevuelveBadRequest()
        {
            var mala = new Respuesta<ResultadoReporte> { Resultado = false, Mensaje = "El filtro 'Cliente' es obligatorio." };
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());
            _app.Setup(a => a.EjecutarAsync("ventas", It.IsAny<SolicitudReporte>())).ReturnsAsync(mala);

            var r = await Crear(Usuario(superusuario: true)).Ejecutar("ventas", new SolicitudReporte());

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>(r.Result).Value);
        }
    }
}
