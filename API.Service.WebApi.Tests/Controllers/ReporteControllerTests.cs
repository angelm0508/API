using System.Security.Claims;
using System.Text.Json;
using API.Application.DTO;
using API.Application.DTO.reporte;
using API.Application.Interface;
using API.Domain.Interface.Reportes;
using API.Service.WebApi.Authorization;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
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

        private ReporteController Crear(ClaimsPrincipal? usuario = null, IConfiguration? config = null) =>
            new(_app.Object, config ?? new ConfigurationBuilder().Build())
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

        // ---------- exportar ----------

        private static ArchivoReporteDTO Archivo() => new() { Contenido = new byte[] { 9, 8 }, ContentType = "text/csv", NombreArchivo = "ventas-20261108-1030.csv" };

        [Fact]
        public async Task Exportar_ConPermiso_DevuelveElArchivoYUsaLaEmpresaConfigurada()
        {
            var req = new ExportarReporteRequest();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Reportes:Empresa"] = "Mi Empresa" }).Build();
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());
            _app.Setup(a => a.ExportarAsync("ventas", req, "csv", "Mi Empresa", "tester"))
                .ReturnsAsync(new Respuesta<ArchivoReporteDTO> { Resultado = true, Dato = Archivo() });

            var r = await Crear(Usuario("ReportesVentas:V"), config).Exportar("ventas", "csv", req);

            var archivo = Assert.IsType<FileContentResult>(r);
            Assert.Equal("text/csv", archivo.ContentType);
            Assert.Equal("ventas-20261108-1030.csv", archivo.FileDownloadName);
            Assert.Equal(new byte[] { 9, 8 }, archivo.FileContents);
        }

        [Fact]
        public async Task Exportar_SinEmpresaConfigurada_UsaSistemaDeGestion()
        {
            var req = new ExportarReporteRequest();
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());
            _app.Setup(a => a.ExportarAsync("ventas", req, "csv", "Sistema de Gestión", "tester"))
                .ReturnsAsync(new Respuesta<ArchivoReporteDTO> { Resultado = true, Dato = Archivo() });

            var r = await Crear(Usuario(superusuario: true)).Exportar("ventas", "csv", req);

            Assert.IsType<FileContentResult>(r);
        }

        [Fact]
        public async Task Exportar_SinPermiso_Devuelve403YNoGeneraNada()
        {
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());

            var r = await Crear(Usuario("ReportesCompras:V")).Exportar("ventas", "csv", new ExportarReporteRequest());

            Es403(r);
            _app.Verify(a => a.ExportarAsync(It.IsAny<string>(), It.IsAny<ExportarReporteRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Exportar_ErrorDeNegocio_DevuelveBadRequest()
        {
            var mala = new Respuesta<ArchivoReporteDTO> { Resultado = false, Mensaje = "El formato 'x' no es válido. Use xlsx, pdf o csv." };
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());
            _app.Setup(a => a.ExportarAsync("ventas", It.IsAny<ExportarReporteRequest>(), "x", It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(mala);

            var r = await Crear(Usuario(superusuario: true)).Exportar("ventas", "x", new ExportarReporteRequest());

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>(r).Value);
        }

        [Fact]
        public async Task Exportar_ReporteInexistente_DevuelveNotFound()
        {
            _app.Setup(a => a.ObtenerDefinicion("x")).Returns(new Respuesta<ReporteDefinicion> { Resultado = false, Mensaje = "El reporte 'x' no existe." });

            var r = await Crear().Exportar("x", "csv", new ExportarReporteRequest());

            Assert.IsType<NotFoundObjectResult>(r);
        }

        [Fact]
        public async Task Exportar_CuerpoNulo_DevuelveBadRequest()
        {
            var r = await Crear().Exportar("ventas", "csv", null!);

            Assert.IsType<BadRequestObjectResult>(r);
        }

        // ---------- vistas guardadas ----------

        private static ClaimsPrincipal SinId() =>
            new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "tester"), new Claim("SuperUsuario", "S") }, "Test"));

        [Fact]
        public async Task Vistas_ConPermiso_ListaLasDelUsuarioDelToken()
        {
            var buena = new Respuesta<List<ReporteVistaDTO>> { Resultado = true, Dato = new() };
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());
            _app.Setup(a => a.ObtenerVistasAsync(5, "ventas")).ReturnsAsync(buena);

            var r = await Crear(Usuario("ReportesVentas:V", id: "5")).Vistas("ventas");

            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task Vistas_SinPermiso_Devuelve403()
        {
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());

            var r = await Crear(Usuario("ReportesCompras:V")).Vistas("ventas");

            Es403(r.Result!);
        }

        [Fact]
        public async Task Vistas_SinIdentificadorDeUsuario_Devuelve401()
        {
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());

            var r = await Crear(SinId()).Vistas("ventas");

            var no = Assert.IsType<UnauthorizedObjectResult>(r.Result);
            Assert.Equal("No se pudo identificar al usuario.", ((Respuesta<List<ReporteVistaDTO>>)no.Value!).Mensaje);
        }

        [Fact]
        public async Task GuardarVista_Ok_DevuelveOk()
        {
            var vista = new GuardarVistaDTO { Nombre = "V", Configuracion = JsonDocument.Parse("{}").RootElement.Clone() };
            var buena = new Respuesta<ReporteVistaDTO> { Resultado = true, Dato = new ReporteVistaDTO { Entry = 3 } };
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());
            _app.Setup(a => a.GuardarVistaAsync(5, "ventas", vista)).ReturnsAsync(buena);

            var r = await Crear(Usuario("ReportesVentas:V", id: "5")).GuardarVista("ventas", vista);

            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task GuardarVista_ErrorDeNegocio_DevuelveBadRequest()
        {
            var mala = new Respuesta<ReporteVistaDTO> { Resultado = false, Mensaje = "El nombre de la vista es obligatorio." };
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());
            _app.Setup(a => a.GuardarVistaAsync(It.IsAny<int>(), "ventas", It.IsAny<GuardarVistaDTO>())).ReturnsAsync(mala);

            var r = await Crear(Usuario(superusuario: true)).GuardarVista("ventas", new GuardarVistaDTO());

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task GuardarVista_CuerpoNulo_DevuelveBadRequest()
        {
            var r = await Crear().GuardarVista("ventas", null!);

            Assert.IsType<BadRequestObjectResult>(r.Result);
        }

        [Fact]
        public async Task EliminarVista_Ok_DevuelveOk()
        {
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());
            _app.Setup(a => a.EliminarVistaAsync(5, "ventas", 9)).ReturnsAsync(buena);

            var r = await Crear(Usuario("ReportesVentas:V", id: "5")).EliminarVista("ventas", 9);

            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task EliminarVista_Inexistente_DevuelveNotFound()
        {
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "La vista no existe." };
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());
            _app.Setup(a => a.EliminarVistaAsync(It.IsAny<int>(), "ventas", 9)).ReturnsAsync(mala);

            var r = await Crear(Usuario(superusuario: true)).EliminarVista("ventas", 9);

            Assert.Same(mala, Assert.IsType<NotFoundObjectResult>(r.Result).Value);
        }
    }
}
