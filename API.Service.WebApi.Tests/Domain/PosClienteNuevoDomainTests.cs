using API.Domain.Core.Pos;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Domain.Interface.Pos;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    /// <summary>
    /// «Cliente nuevo» rápido del POS: valida todo antes de numerar (un rechazo no llama a la inserción del socio, así no
    /// consume consecutivo) y crea el cliente con el dominio de socios en la serie automática por defecto de clientes.
    /// </summary>
    public class PosClienteNuevoDomainTests
    {
        private const int SerieClientes = 7;

        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocio = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoSerie = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumento, string>> _repoNumeracion = new();
        private readonly Mock<ISocioNegocioDomain> _socios = new();
        private readonly PosDomain _domain;
        private SocioNegocio? _insertado;

        public PosClienteNuevoDomainTests()
        {
            Socios();
            Encabezados(new NumeracionDocumento { CodigoObj = "1", SubTipoDoc = "C", SerieDfct = SerieClientes },
                new NumeracionDocumento { CodigoObj = "1", SubTipoDoc = "P", SerieDfct = 8 });
            _repoSerie.Setup(r => r.ObtenerAsync(SerieClientes)).ReturnsAsync(SerieAutomatica());
            _socios.Setup(s => s.InsertarAsync(It.IsAny<SocioNegocio>()))
                .Callback<SocioNegocio>(s => _insertado = s)
                .ReturnsAsync("CLI-0005");

            _domain = new PosDomain(new Mock<IRepositorioGenerico<PosConfiguracion, int>>().Object, _repoSerie.Object,
                new Mock<IRepositorioGenerico<Almacen, string>>().Object, new Mock<IRepositorioGenerico<Impuesto, string>>().Object,
                _repoSocio.Object, new Mock<IRepositorioGenerico<FormaPago, string>>().Object,
                new Mock<IRepositorioGenerico<Monedum, string>>().Object,
                new Mock<IRepositorioGenerico<Articulo, string>>().Object,
                new Mock<IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)>>().Object,
                new Mock<IPrecioListaDomain>().Object,
                new Mock<IFacturaDomain>().Object,
                new Mock<IPagoRecibidoDomain>().Object,
                new Mock<IEjecutorTransaccion>().Object,
                new Mock<IRepositorioGenerico<Factura, int>>().Object,
                new Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>>().Object,
                new Mock<IRepositorioGenerico<PagoRecibido, int>>().Object,
                new Mock<IRepositorioGenerico<PagoRecibidoFactura, (int Entry, int NoLinea)>>().Object,
                new Mock<IRepositorioGenerico<PagoRecibidoMedio, (int Entry, int NoLinea)>>().Object,
                new Mock<IBloqueoSeries>().Object,
                _repoNumeracion.Object,
                _socios.Object);
        }

        private static NumeracionDocumentoDet SerieAutomatica() => new()
        {
            Serie = SerieClientes, CodigoObj = "1", SubTipoDoc = "C", NombreSerie = "CLI", TipoSerie = "SN",
            Manual = "N", Bloqueado = "N", IniNumero = 1, SigNumero = 5
        };

        private void Socios(params SocioNegocio[] socios) =>
            _repoSocio.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(socios.AsAsyncQueryable());

        private void Encabezados(params NumeracionDocumento[] filas) =>
            _repoNumeracion.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(filas.AsAsyncQueryable());

        private async Task Rechaza(NuevoClientePos solicitud, string mensaje)
        {
            var ex = await Assert.ThrowsAnyAsync<Exception>(() => _domain.CrearClienteAsync(solicitud));
            Assert.Equal(mensaje, ex.Message);
            _socios.Verify(s => s.InsertarAsync(It.IsAny<SocioNegocio>()), Times.Never);
        }

        // ---------- éxito ----------

        [Fact]
        public async Task Exito_CreaElClienteDeContadoEnLaSerieAutomaticaPorDefecto_YLoDevuelve()
        {
            var c = await _domain.CrearClienteAsync(new NuevoClientePos("  Juan Pérez  ", " 1234567-8 "));

            Assert.Equal(new ClientePos("CLI-0005", "Juan Pérez", "1234567-8", null), c);
            _socios.Verify(s => s.InsertarAsync(It.IsAny<SocioNegocio>()), Times.Once);
            var s = _insertado!;
            Assert.Equal("C", s.TipoSn);
            Assert.Equal("S", s.Activo);
            Assert.Equal("Juan Pérez", s.Nombre);
            Assert.Equal("1234567-8", s.Nit);
            Assert.Equal(SerieClientes, s.Serie);
            Assert.Equal(0m, s.LimiteCredito);
            Assert.Equal("N", s.BloquearSiVencido);
            Assert.Null(s.CondicionPago);
            Assert.Null(s.GrupoSn);
            Assert.Null(s.NumLstPrecio);
            Assert.Null(s.Tel1);
            Assert.Null(s.Tel2);
            Assert.Null(s.Email);
            Assert.Null(s.PersContacto);
            Assert.Null(s.Cui);
            Assert.Null(s.Descuento);
            Assert.Empty(s.DireccionSocioNegocios);
        }

        [Fact]
        public async Task Exito_NombreDeExactamenteElMaximo_SeAcepta()
        {
            var nombre = new string('a', PosDomain.LongitudMaximaNombreCliente);

            var c = await _domain.CrearClienteAsync(new NuevoClientePos(nombre, "55"));

            Assert.Equal(nombre, c.Nombre);
        }

        [Fact]
        public void LongitudMaximaDelNombre_EsLaDeLaColumnaNombreDeSocioNegocio()
        {
            var opciones = new DbContextOptionsBuilder<ApiDbTestContext>()
                .UseSqlServer("Server=.;Database=x;Trusted_Connection=True;TrustServerCertificate=True")
                .Options;
            using var contexto = new ApiDbTestContext(opciones);

            var maximo = contexto.Model.FindEntityType(typeof(SocioNegocio))!.FindProperty(nameof(SocioNegocio.Nombre))!.GetMaxLength();

            Assert.Equal(maximo, PosDomain.LongitudMaximaNombreCliente);
        }

        // ---------- nombre ----------

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task NombreVacio_Rechaza(string? nombre) =>
            await Rechaza(new NuevoClientePos(nombre, "1234567-8"), "El nombre del cliente es obligatorio.");

        [Fact]
        public async Task NombreMasLargoQueLaColumna_Rechaza() =>
            await Rechaza(new NuevoClientePos(new string('a', PosDomain.LongitudMaximaNombreCliente + 1), "1234567-8"),
                $"El nombre del cliente no puede tener más de {PosDomain.LongitudMaximaNombreCliente} caracteres.");

        [Fact]
        public async Task NombreConEspaciosAlrededor_CuentaRecortado() =>
            Assert.Equal("CLI-0005", (await _domain.CrearClienteAsync(
                new NuevoClientePos("  " + new string('a', PosDomain.LongitudMaximaNombreCliente) + "  ", "1"))).Codigo);

        // ---------- NIT ----------

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task NitVacio_Rechaza(string? nit) =>
            await Rechaza(new NuevoClientePos("Juan", nit), "El NIT es obligatorio (escribe CF si es consumidor final).");

        [Fact]
        public async Task NitDeMasDe15Caracteres_Rechaza() =>
            await Rechaza(new NuevoClientePos("Juan", "1234567890123456"), "El NIT no puede tener más de 15 caracteres.");

        [Fact]
        public async Task NombreVacioYNitVacio_PrimeroElNombre() =>
            await Rechaza(new NuevoClientePos(" ", " "), "El nombre del cliente es obligatorio.");

        // ---------- duplicado ----------

        [Fact]
        public async Task NitDeOtroCliente_Rechaza()
        {
            Socios(new SocioNegocio { Codigo = "C-9", Nombre = "Ana López", TipoSn = "C", Activo = "S", Nit = " 1234567-8 " });

            await Rechaza(new NuevoClientePos("Juan", "1234567-8"),
                "Ya existe un cliente con el NIT 1234567-8: C-9 - Ana López. Búscalo por NIT.");
        }

        [Fact]
        public async Task NitDeOtroClienteSinNombre_MuestraElCodigo()
        {
            Socios(new SocioNegocio { Codigo = "C-9", Nombre = null, TipoSn = "C", Activo = "S", Nit = "99" });

            await Rechaza(new NuevoClientePos("Juan", "99"), "Ya existe un cliente con el NIT 99: C-9 - C-9. Búscalo por NIT.");
        }

        [Fact]
        public async Task NitDeUnProveedor_NoEsDuplicado()
        {
            Socios(new SocioNegocio { Codigo = "P-1", Nombre = "Proveedor", TipoSn = "P", Activo = "S", Nit = "1234567-8" });

            Assert.Equal("CLI-0005", (await _domain.CrearClienteAsync(new NuevoClientePos("Juan", "1234567-8"))).Codigo);
        }

        [Theory]
        [InlineData("CF", "CF")]
        [InlineData(" cf ", "CF")]
        [InlineData("Cf", "CF")]
        public async Task NitCF_SePermiteRepetido_YSeGuardaEnMayusculas(string nit, string guardado)
        {
            Socios(new SocioNegocio { Codigo = "CF", Nombre = "Consumidor Final", TipoSn = "C", Activo = "S", Nit = "CF" });

            var c = await _domain.CrearClienteAsync(new NuevoClientePos("Juan", nit));

            Assert.Equal(guardado, c.Nit);
            Assert.Equal(guardado, _insertado!.Nit);
        }

        // ---------- serie por defecto de clientes ----------

        private const string SinSerie = "No hay una serie automática por defecto para clientes: configúrala en Numeración de documentos.";

        [Fact]
        public async Task SinEncabezadoDeNumeracionDeClientes_Rechaza()
        {
            Encabezados(new NumeracionDocumento { CodigoObj = "1", SubTipoDoc = "P", SerieDfct = 8 });

            await Rechaza(new NuevoClientePos("Juan", "1"), SinSerie);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        public async Task EncabezadoSinSeriePorDefecto_Rechaza(int? serie)
        {
            Encabezados(new NumeracionDocumento { CodigoObj = "1", SubTipoDoc = "C", SerieDfct = serie });

            await Rechaza(new NuevoClientePos("Juan", "1"), SinSerie);
        }

        [Fact]
        public async Task SeriePorDefectoInexistente_Rechaza()
        {
            _repoSerie.Setup(r => r.ObtenerAsync(SerieClientes)).ReturnsAsync((NumeracionDocumentoDet?)null);

            await Rechaza(new NuevoClientePos("Juan", "1"), SinSerie);
        }

        [Fact]
        public async Task SeriePorDefectoManual_Rechaza()
        {
            var s = SerieAutomatica(); s.Manual = "S";
            _repoSerie.Setup(r => r.ObtenerAsync(SerieClientes)).ReturnsAsync(s);

            await Rechaza(new NuevoClientePos("Juan", "1"), SinSerie);
        }

        [Fact]
        public async Task SeriePorDefectoBloqueada_Rechaza()
        {
            var s = SerieAutomatica(); s.Bloqueado = "S";
            _repoSerie.Setup(r => r.ObtenerAsync(SerieClientes)).ReturnsAsync(s);

            await Rechaza(new NuevoClientePos("Juan", "1"), SinSerie);
        }

        [Fact]
        public async Task SeriePorDefectoDeOtroDocumento_Rechaza()
        {
            var s = SerieAutomatica(); s.CodigoObj = "6"; s.SubTipoDoc = "--";
            _repoSerie.Setup(r => r.ObtenerAsync(SerieClientes)).ReturnsAsync(s);

            await Rechaza(new NuevoClientePos("Juan", "1"), SinSerie);
        }

        [Fact]
        public async Task ElRechazoDelDominioDeSocios_SePropagaConSuMensaje()
        {
            _socios.Setup(s => s.InsertarAsync(It.IsAny<SocioNegocio>())).ThrowsAsync(new Exception("Se agotó la numeración disponible en esta serie."));

            var ex = await Assert.ThrowsAnyAsync<Exception>(() => _domain.CrearClienteAsync(new NuevoClientePos("Juan", "1")));

            Assert.Equal("Se agotó la numeración disponible en esta serie.", ex.Message);
        }
    }
}
