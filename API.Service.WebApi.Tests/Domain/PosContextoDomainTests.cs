using API.Domain.Core.Pos;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Domain.Interface.Pos;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    /// <summary>Contexto de carga de la pantalla y búsqueda de clientes del POS (ambos bajo Pos.Ver).</summary>
    public class PosContextoDomainTests
    {
        private readonly Mock<IRepositorioGenerico<PosConfiguracion, int>> _repoConfig = new();
        private readonly Mock<IRepositorioGenerico<Impuesto, string>> _repoImpuesto = new();
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocio = new();
        private readonly Mock<IRepositorioGenerico<FormaPago, string>> _repoFormaPago = new();
        private readonly PosDomain _domain;

        public PosContextoDomainTests()
        {
            _repoFormaPago.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new[]
            {
                new FormaPago { Codigo = "TAR", Nombre = "Tarjeta", TipoDato = "J", Activo = "S" },
                new FormaPago { Codigo = "VIE", Nombre = "Vieja", TipoDato = "N", Activo = "N" },
                new FormaPago { Codigo = "EFE", Nombre = "Efectivo", TipoDato = "N", Activo = "S" }
            }.AsAsyncQueryable());
            _repoSocio.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(Array.Empty<SocioNegocio>().AsAsyncQueryable());

            _domain = new PosDomain(_repoConfig.Object, new Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>>().Object,
                new Mock<IRepositorioGenerico<Almacen, string>>().Object, _repoImpuesto.Object,
                _repoSocio.Object, _repoFormaPago.Object, new Mock<IRepositorioGenerico<Monedum, string>>().Object,
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
                new Mock<IRepositorioGenerico<NumeracionDocumento, string>>().Object,
                new Mock<ISocioNegocioDomain>().Object);
        }

        private void ConfigCompleta() =>
            _repoConfig.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new PosConfiguracion
            {
                Id = 1, SerieFactura = 10, SeriePago = 20, CodAlmacen = "ALM1", CodigoImpuesto = "IVA",
                ClienteDefecto = "CF1", FormaPagoEfectivo = "EFE", MonedaDoc = "GTQ"
            });

        // ---------- Contexto ----------

        [Fact]
        public async Task Contexto_Completo_TraeConfiguracionFormasActivasTasaYClientePorDefecto()
        {
            ConfigCompleta();
            _repoImpuesto.Setup(r => r.ObtenerAsync("IVA")).ReturnsAsync(new Impuesto { Codigo = "IVA", Nombre = "IVA", Tasa = 12m });
            _repoSocio.Setup(r => r.ObtenerAsync("CF1")).ReturnsAsync(new SocioNegocio { Codigo = "CF1", Nombre = "Consumidor Final", Nit = " 123-4 ", TipoSn = "C" });

            var c = await _domain.ObtenerContextoAsync();

            Assert.Empty(c.Configuracion.Faltantes);
            Assert.Equal("CF1", c.Configuracion.Configuracion.ClienteDefecto);
            // Solo las activas, por código.
            Assert.Equal(new[] { new FormaPagoPos("EFE", "Efectivo", "N"), new FormaPagoPos("TAR", "Tarjeta", "J") }, c.FormasPago);
            Assert.Equal(12m, c.TasaImpuesto);
            Assert.Equal("Consumidor Final", c.NombreCliente);
            Assert.Equal("123-4", c.NitCliente);
        }

        [Fact]
        public async Task Contexto_ImpuestoSinTasaOInexistente_TasaCero_YClienteInexistente_NombreYNitNulos()
        {
            ConfigCompleta();
            _repoImpuesto.Setup(r => r.ObtenerAsync("IVA")).ReturnsAsync(new Impuesto { Codigo = "IVA", Nombre = "IVA", Tasa = null });
            _repoSocio.Setup(r => r.ObtenerAsync("CF1")).ReturnsAsync((SocioNegocio?)null);

            var c = await _domain.ObtenerContextoAsync();
            Assert.Equal(0m, c.TasaImpuesto);
            Assert.Null(c.NombreCliente);
            Assert.Null(c.NitCliente);

            _repoImpuesto.Setup(r => r.ObtenerAsync("IVA")).ReturnsAsync((Impuesto?)null);
            Assert.Equal(0m, (await _domain.ObtenerContextoAsync()).TasaImpuesto);
        }

        [Fact]
        public async Task Contexto_ConfiguracionIncompleta_DevuelveLosFaltantesSinLanzarNiConsultarImpuestoNiCliente()
        {
            _repoConfig.Setup(r => r.ObtenerAsync(1)).ReturnsAsync((PosConfiguracion?)null);

            var c = await _domain.ObtenerContextoAsync();

            Assert.Equal(7, c.Configuracion.Faltantes.Count);
            Assert.Equal(0m, c.TasaImpuesto);
            Assert.Null(c.NombreCliente);
            Assert.Null(c.NitCliente);
            Assert.Equal(2, c.FormasPago.Count);
            _repoImpuesto.Verify(r => r.ObtenerAsync(It.IsAny<string>()), Times.Never);
            _repoSocio.Verify(r => r.ObtenerAsync(It.IsAny<string>()), Times.Never);
        }

        // ---------- Clientes ----------

        private void Socios(params SocioNegocio[] socios) =>
            _repoSocio.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(socios.AsAsyncQueryable());

        private static SocioNegocio Socio(string codigo, string? nombre, string tipo = "C", string? activo = "S", string? nit = null, int? lista = null) =>
            new() { Codigo = codigo, Nombre = nombre, TipoSn = tipo, Activo = activo, Nit = nit, NumLstPrecio = lista };

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("J")]
        [InlineData(" J ")]
        public async Task Clientes_TextoVacioODeUnCaracter_ListaVaciaSinConsultar(string? texto)
        {
            Socios(Socio("J1", "Juan"));

            Assert.Empty(await _domain.BuscarClientesAsync(texto));
            _repoSocio.Verify(r => r.ObtenerTodoAsync(), Times.Never);
        }

        [Fact]
        public async Task Clientes_PorCodigoYPorNombre_SoloClientesActivos_ConNitYLista()
        {
            Socios(
                Socio("C-100", "Ana López", nit: " 555 ", lista: 3),
                Socio("C-200", "Pedro Ramos"),
                Socio("P-100", "Proveedor C-100", tipo: "P"),
                Socio("C-300", "Inactivo C-100", activo: "N"),
                Socio("C-400", "Sin estado C-100", activo: null));

            var porCodigo = await _domain.BuscarClientesAsync("C-100");
            Assert.Equal(new[] { "C-100", "C-400" }, porCodigo.Select(c => c.Codigo).ToArray());
            Assert.Equal(new ClientePos("C-100", "Ana López", "555", 3), porCodigo[0]);

            var porNombre = await _domain.BuscarClientesAsync(" Ramos ");
            Assert.Equal(new ClientePos("C-200", "Pedro Ramos", null, null), Assert.Single(porNombre));
        }

        [Fact]
        public async Task Clientes_TopeDe10_PorNombreYCodigo()
        {
            Socios(Enumerable.Range(0, 15).Select(i => Socio("K" + i.ToString("00"), "Cliente " + (14 - i).ToString("00"))).ToArray());

            var r = await _domain.BuscarClientesAsync("Cliente");

            Assert.Equal(10, r.Count);
            Assert.Equal("K14", r[0].Codigo);   // «Cliente 00»
            Assert.Equal("K05", r[9].Codigo);   // «Cliente 09»
        }

        [Fact]
        public async Task Clientes_PorNit_ParcialYCompleto_SoloClientesActivos()
        {
            Socios(
                Socio("C-100", "Ana López", nit: "1234567-8"),
                Socio("C-200", "Pedro Ramos", nit: "765432K"),
                Socio("C-300", "Sin NIT", nit: null),
                Socio("P-100", "Proveedor", tipo: "P", nit: "1234567-8"),
                Socio("C-400", "Inactivo", activo: "N", nit: "1234567-8"));

            var completo = await _domain.BuscarClientesAsync("1234567-8");
            Assert.Equal(new ClientePos("C-100", "Ana López", "1234567-8", null), Assert.Single(completo));

            var parcial = await _domain.BuscarClientesAsync(" 2K ");
            Assert.Equal("C-200", Assert.Single(parcial).Codigo);

            // «45» está en el NIT de C-100 (no en códigos ni nombres) y solo sale el cliente activo.
            Assert.Equal(new[] { "C-100" }, (await _domain.BuscarClientesAsync("45")).Select(c => c.Codigo).ToArray());
        }

        [Fact]
        public async Task Clientes_SinNombre_UsaElCodigo()
        {
            Socios(Socio("SN-1", null));

            Assert.Equal("SN-1", Assert.Single(await _domain.BuscarClientesAsync("SN")).Nombre);
        }
    }
}
