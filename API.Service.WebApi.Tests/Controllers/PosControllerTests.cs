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
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class PosControllerTests
    {
        private readonly Mock<IPosApplication> _app = new();

        private PosController Crear(ClaimsPrincipal? usuario = null, IConfiguration? config = null) =>
            new(_app.Object, config ?? new ConfigurationBuilder().Build())
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

        // ---------- Catálogo ----------

        private static readonly ArticuloPos Art1 = new("A1", "123", "Aceite", 50m, 12m, true);

        private static Respuesta<IReadOnlyList<ArticuloPos>> Lista() => new()
        {
            Resultado = true, Mensaje = "ok", Dato = new List<ArticuloPos> { Art1 }
        };

        [Fact]
        public async Task BuscarArticulos_Exito_Devuelve200YPasaTextoYCliente()
        {
            var buena = Lista();
            _app.Setup(a => a.BuscarArticulosAsync("ace", "C1")).ReturnsAsync(buena);

            var r = await Crear().BuscarArticulos("ace", "C1");

            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task BuscarArticulos_ConError_Devuelve400()
        {
            _app.Setup(a => a.BuscarArticulosAsync("ace", null))
                .ReturnsAsync(new Respuesta<IReadOnlyList<ArticuloPos>> { Mensaje = "fallo" });

            var r = await Crear().BuscarArticulos("ace", null);

            Assert.IsType<BadRequestObjectResult>(r.Result);
        }

        [Fact]
        public async Task ObtenerArticuloPorCodigo_Existe_Devuelve200()
        {
            var buena = new Respuesta<ArticuloPos?> { Resultado = true, Mensaje = "ok", Dato = Art1 };
            _app.Setup(a => a.ObtenerArticuloPorCodigoAsync("123", "C1")).ReturnsAsync(buena);

            var r = await Crear().ObtenerArticuloPorCodigo("123", "C1");

            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task ObtenerArticuloPorCodigo_NoExisteONoVendible_Devuelve404()
        {
            _app.Setup(a => a.ObtenerArticuloPorCodigoAsync("999", null))
                .ReturnsAsync(new Respuesta<ArticuloPos?> { Resultado = true, Mensaje = "ok", Dato = null });

            var r = await Crear().ObtenerArticuloPorCodigo("999", null);

            var nf = Assert.IsType<NotFoundObjectResult>(r.Result);
            Assert.False(((Respuesta<ArticuloPos?>)nf.Value!).Resultado);
            Assert.Equal("El artículo 999 no existe o no está disponible para la venta.", ((Respuesta<ArticuloPos?>)nf.Value!).Mensaje);
        }

        [Fact]
        public async Task ObtenerArticuloPorCodigo_ConError_Devuelve400()
        {
            _app.Setup(a => a.ObtenerArticuloPorCodigoAsync("1", null))
                .ReturnsAsync(new Respuesta<ArticuloPos?> { Mensaje = "fallo" });

            var r = await Crear().ObtenerArticuloPorCodigo("1", null);

            Assert.IsType<BadRequestObjectResult>(r.Result);
        }

        [Fact]
        public async Task ObtenerRapidos_Exito_Devuelve200()
        {
            var buena = Lista();
            _app.Setup(a => a.ObtenerRapidosAsync()).ReturnsAsync(buena);

            var r = await Crear().ObtenerRapidos();

            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task ObtenerRapidos_ConError_Devuelve400()
        {
            _app.Setup(a => a.ObtenerRapidosAsync()).ReturnsAsync(new Respuesta<IReadOnlyList<ArticuloPos>> { Mensaje = "fallo" });

            var r = await Crear().ObtenerRapidos();

            Assert.IsType<BadRequestObjectResult>(r.Result);
        }

        [Theory]
        [InlineData(nameof(PosController.BuscarArticulos), "Articulos")]
        [InlineData(nameof(PosController.ObtenerArticuloPorCodigo), "Articulos/porCodigo/{codigo}")]
        [InlineData(nameof(PosController.ObtenerRapidos), "Rapidos")]
        public void Catalogo_LlevaElPermisoVerDePosYLaRutaEsperada(string metodo, string ruta)
        {
            var m = typeof(PosController).GetMethod(metodo)!;
            Assert.Equal(ruta, m.GetCustomAttribute<HttpGetAttribute>()!.Template);
            var atributo = Assert.Single(m.GetCustomAttributes<RequierePermisoAttribute>());
            Assert.Equal(("Pos", Accion.Ver), Permiso(atributo));
        }

        // ---------- Venta, ticket y ventas del día ----------

        private static readonly VentaPosSolicitud Solicitud = new(null, null,
            new List<VentaPosLinea> { new("A1", 1m) }, new List<VentaPosPago> { new("EFE", 50m, null, null, null) });

        private static TicketPos Ticket() => new(501, 10, "POS", 77, DateTime.Today, "tester", "C1", "Cliente", "CF",
            Array.Empty<TicketPosLinea>(), 10m, 1.2m, 11.2m, Array.Empty<TicketPosPago>(), 50m, 38.8m, "GTQ", string.Empty);

        private static IConfiguration ConEmpresa(string empresa) =>
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Reportes:Empresa"] = empresa }).Build();

        private static ClaimsPrincipal UsuarioSinNombre(string? nombre)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "5"), new("perm", "Pos:A") };
            if (nombre is not null) claims.Add(new Claim(ClaimTypes.Name, nombre));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        }

        [Fact]
        public async Task Vender_Exito_PasaElCajeroDelToken_YCompletaLaEmpresaDeLaConfiguracion()
        {
            _app.Setup(a => a.VenderAsync(Solicitud, "tester"))
                .ReturnsAsync(new Respuesta<TicketPos> { Resultado = true, Mensaje = "ok", Dato = Ticket() });

            var r = await Crear(config: ConEmpresa("Mi Empresa")).Vender(Solicitud);

            var ok = (Respuesta<TicketPos>)Assert.IsType<OkObjectResult>(r.Result).Value!;
            Assert.True(ok.Resultado);
            Assert.Equal("Mi Empresa", ok.Dato!.Empresa);
            Assert.Equal(501, ok.Dato.FacturaEntry);
        }

        [Fact]
        public async Task Vender_SinEmpresaConfigurada_UsaSistemaDeGestion()
        {
            _app.Setup(a => a.VenderAsync(Solicitud, "tester"))
                .ReturnsAsync(new Respuesta<TicketPos> { Resultado = true, Mensaje = "ok", Dato = Ticket() });

            var r = await Crear().Vender(Solicitud);

            Assert.Equal("Sistema de Gestión", ((Respuesta<TicketPos>)Assert.IsType<OkObjectResult>(r.Result).Value!).Dato!.Empresa);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Vender_SinCajeroEnElToken_Devuelve400SinTocarLaAplicacion(string? nombre)
        {
            var r = await Crear(UsuarioSinNombre(nombre)).Vender(Solicitud);

            var bad = Assert.IsType<BadRequestObjectResult>(r.Result);
            Assert.Equal("No se pudo identificar al cajero.", ((Respuesta<TicketPos>)bad.Value!).Mensaje);
            _app.Verify(a => a.VenderAsync(It.IsAny<VentaPosSolicitud>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Vender_CuerpoNulo_Devuelve400SinTocarLaAplicacion()
        {
            var r = await Crear().Vender(null);

            Assert.IsType<BadRequestObjectResult>(r.Result);
            _app.Verify(a => a.VenderAsync(It.IsAny<VentaPosSolicitud>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Vender_ConError_Devuelve400ConElMensaje()
        {
            _app.Setup(a => a.VenderAsync(Solicitud, "tester"))
                .ReturnsAsync(new Respuesta<TicketPos> { Mensaje = "El pago (50.00) no cubre el total (60.00)." });

            var r = await Crear().Vender(Solicitud);

            var bad = Assert.IsType<BadRequestObjectResult>(r.Result);
            Assert.Equal("El pago (50.00) no cubre el total (60.00).", ((Respuesta<TicketPos>)bad.Value!).Mensaje);
        }

        [Fact]
        public async Task Ticket_Exito_CompletaLaEmpresa()
        {
            _app.Setup(a => a.ObtenerTicketAsync(501))
                .ReturnsAsync(new Respuesta<TicketPos> { Resultado = true, Mensaje = "ok", Dato = Ticket() });

            var r = await Crear(config: ConEmpresa("Taller XYZ")).ObtenerTicket(501);

            Assert.Equal("Taller XYZ", ((Respuesta<TicketPos>)Assert.IsType<OkObjectResult>(r.Result).Value!).Dato!.Empresa);
        }

        [Fact]
        public async Task Ticket_Inexistente_Devuelve404()
        {
            _app.Setup(a => a.ObtenerTicketAsync(9)).ReturnsAsync(new Respuesta<TicketPos> { Mensaje = "La venta no existe." });

            var r = await Crear().ObtenerTicket(9);

            var nf = Assert.IsType<NotFoundObjectResult>(r.Result);
            Assert.Equal("La venta no existe.", ((Respuesta<TicketPos>)nf.Value!).Mensaje);
        }

        [Fact]
        public async Task Ticket_OtroError_Devuelve400()
        {
            _app.Setup(a => a.ObtenerTicketAsync(9)).ReturnsAsync(new Respuesta<TicketPos> { Mensaje = "fallo" });

            var r = await Crear().ObtenerTicket(9);

            Assert.IsType<BadRequestObjectResult>(r.Result);
        }

        [Fact]
        public async Task VentasDelDia_Exito_Devuelve200_YConError400()
        {
            var buena = new Respuesta<IReadOnlyList<VentaPosDelDia>> { Resultado = true, Mensaje = "ok", Dato = Array.Empty<VentaPosDelDia>() };
            _app.Setup(a => a.ObtenerVentasDelDiaAsync()).ReturnsAsync(buena);

            var r = await Crear().ObtenerVentasDelDia();
            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);

            _app.Setup(a => a.ObtenerVentasDelDiaAsync()).ReturnsAsync(new Respuesta<IReadOnlyList<VentaPosDelDia>> { Mensaje = "fallo" });
            Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerVentasDelDia()).Result);
        }

        [Fact]
        public void Venta_LlevaElPermisoAgregarDePos_YEsPost()
        {
            var m = typeof(PosController).GetMethod(nameof(PosController.Vender))!;
            Assert.Equal("Venta", m.GetCustomAttribute<HttpPostAttribute>()!.Template);
            var atributo = Assert.Single(m.GetCustomAttributes<RequierePermisoAttribute>());
            Assert.Equal(("Pos", Accion.Agregar), Permiso(atributo));
        }

        [Theory]
        [InlineData(nameof(PosController.ObtenerTicket), "Ticket/{facturaEntry}")]
        [InlineData(nameof(PosController.ObtenerVentasDelDia), "VentasDelDia")]
        public void TicketYVentasDelDia_LlevanElPermisoVerDePos(string metodo, string ruta)
        {
            var m = typeof(PosController).GetMethod(metodo)!;
            Assert.Equal(ruta, m.GetCustomAttribute<HttpGetAttribute>()!.Template);
            var atributo = Assert.Single(m.GetCustomAttributes<RequierePermisoAttribute>());
            Assert.Equal(("Pos", Accion.Ver), Permiso(atributo));
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
