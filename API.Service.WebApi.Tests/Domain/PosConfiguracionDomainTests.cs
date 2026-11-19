using API.Domain.Core.Pos;
using API.Domain.Core.Reportes.Compartido;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Domain.Interface.Pos;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class PosConfiguracionDomainTests
    {
        private readonly Mock<IRepositorioGenerico<PosConfiguracion, int>> _repoConfig = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoSerie = new();
        private readonly Mock<IRepositorioGenerico<Almacen, string>> _repoAlmacen = new();
        private readonly Mock<IRepositorioGenerico<Impuesto, string>> _repoImpuesto = new();
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocio = new();
        private readonly Mock<IRepositorioGenerico<FormaPago, string>> _repoFormaPago = new();
        private readonly Mock<IRepositorioGenerico<Monedum, string>> _repoMoneda = new();
        private readonly PosDomain _domain;

        public PosConfiguracionDomainTests()
        {
            _domain = new PosDomain(_repoConfig.Object, _repoSerie.Object, _repoAlmacen.Object, _repoImpuesto.Object,
                _repoSocio.Object, _repoFormaPago.Object, _repoMoneda.Object,
                new Mock<IRepositorioGenerico<Articulo, string>>().Object,
                new Mock<IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)>>().Object,
                new Mock<IPrecioListaDomain>().Object,
                new ConsultaLineasVenta(new Mock<IRepositorioGenerico<Factura, int>>().Object,
                    new Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>>().Object,
                    new Mock<IRepositorioGenerico<MovimientoInventario, int>>().Object),
                new Mock<IFacturaDomain>().Object,
                new Mock<IPagoRecibidoDomain>().Object,
                new Mock<IEjecutorTransaccion>().Object,
                new Mock<IRepositorioGenerico<Factura, int>>().Object,
                new Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>>().Object,
                new Mock<IRepositorioGenerico<PagoRecibido, int>>().Object,
                new Mock<IRepositorioGenerico<PagoRecibidoFactura, (int Entry, int NoLinea)>>().Object,
                new Mock<IRepositorioGenerico<PagoRecibidoMedio, (int Entry, int NoLinea)>>().Object,
                new Mock<IBloqueoSeries>().Object);
        }

        private static NumeracionDocumentoDet Serie(int serie, string codigoObj, string subTipo = "--", string? manual = "N", string? bloqueado = "N") => new()
        {
            Serie = serie,
            CodigoObj = codigoObj,
            SubTipoDoc = subTipo,
            Manual = manual,
            Bloqueado = bloqueado,
            NombreSerie = "S" + serie
        };

        private static readonly PosConfiguracionDato Completa = new(10, 20, "ALM1", "IVA", "C0001", "EFE", "GTQ");

        /// <summary>Deja que todas las referencias de <see cref="Completa"/> existan y sean válidas.</summary>
        private void TodoValido()
        {
            _repoSerie.Setup(r => r.ObtenerAsync(10)).ReturnsAsync(Serie(10, "6"));
            _repoSerie.Setup(r => r.ObtenerAsync(20)).ReturnsAsync(Serie(20, "24"));
            _repoAlmacen.Setup(r => r.ObtenerAsync("ALM1")).ReturnsAsync(new Almacen { Codigo = "ALM1", Activo = "S" });
            _repoImpuesto.Setup(r => r.ObtenerAsync("IVA")).ReturnsAsync(new Impuesto { Codigo = "IVA" });
            _repoSocio.Setup(r => r.ObtenerAsync("C0001")).ReturnsAsync(new SocioNegocio { Codigo = "C0001", TipoSn = "C" });
            _repoFormaPago.Setup(r => r.ObtenerAsync("EFE")).ReturnsAsync(new FormaPago { Codigo = "EFE", Nombre = "Efectivo", Activo = "S" });
            _repoMoneda.Setup(r => r.ObtenerAsync("GTQ")).ReturnsAsync(new Monedum { Codigo = "GTQ" });
        }

        // ---------- ObtenerConfiguracion ----------

        [Fact]
        public async Task Obtener_SinFila_DevuelveVaciaConTodosLosFaltantesEnOrden()
        {
            _repoConfig.Setup(r => r.ObtenerAsync(1)).ReturnsAsync((PosConfiguracion?)null);

            var vista = await _domain.ObtenerConfiguracionAsync();

            Assert.Equal(new PosConfiguracionDato(null, null, null, null, null, null, null), vista.Configuracion);
            Assert.Equal(new[]
            {
                "Serie de factura POS", "Serie de pagos", "Almacén POS", "Impuesto por defecto",
                "Cliente por defecto", "Forma de pago en efectivo", "Moneda"
            }, vista.Faltantes);
        }

        [Fact]
        public async Task Obtener_Completa_NoTieneFaltantes()
        {
            _repoConfig.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new PosConfiguracion
            {
                Id = 1, SerieFactura = 10, SeriePago = 20, CodAlmacen = "ALM1", CodigoImpuesto = "IVA",
                ClienteDefecto = "C0001", FormaPagoEfectivo = "EFE", MonedaDoc = "GTQ"
            });

            var vista = await _domain.ObtenerConfiguracionAsync();

            Assert.Equal(Completa, vista.Configuracion);
            Assert.Empty(vista.Faltantes);
        }

        [Fact]
        public async Task Obtener_Parcial_ListaSoloLoQueFalta()
        {
            _repoConfig.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new PosConfiguracion
            {
                Id = 1, SerieFactura = 10, CodAlmacen = "ALM1", ClienteDefecto = "C0001", MonedaDoc = "GTQ"
            });

            var vista = await _domain.ObtenerConfiguracionAsync();

            Assert.Equal(new[] { "Serie de pagos", "Impuesto por defecto", "Forma de pago en efectivo" }, vista.Faltantes);
        }

        // ---------- Guardar: validaciones ----------

        private async Task AssertRechaza(PosConfiguracionDato dato, string mensaje)
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _domain.GuardarConfiguracionAsync(dato));
            Assert.Equal(mensaje, ex.Message);
            _repoConfig.Verify(r => r.InsertarAsync(It.IsAny<PosConfiguracion>()), Times.Never);
            _repoConfig.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<PosConfiguracion>()), Times.Never);
        }

        private const string MsgSerieFactura = "La serie de factura POS no es válida: debe ser una serie automática y activa de facturas.";
        private const string MsgSeriePago = "La serie de pagos no es válida: debe ser una serie automática y activa de pagos recibidos.";

        [Fact]
        public async Task Guardar_SerieFacturaInexistente_Rechaza()
        {
            TodoValido();
            _repoSerie.Setup(r => r.ObtenerAsync(10)).ReturnsAsync((NumeracionDocumentoDet?)null);
            await AssertRechaza(Completa, MsgSerieFactura);
        }

        [Fact]
        public async Task Guardar_SerieFacturaManual_Rechaza()
        {
            TodoValido();
            _repoSerie.Setup(r => r.ObtenerAsync(10)).ReturnsAsync(Serie(10, "6", manual: "S"));
            await AssertRechaza(Completa, MsgSerieFactura);
        }

        [Fact]
        public async Task Guardar_SerieFacturaBloqueada_Rechaza()
        {
            TodoValido();
            _repoSerie.Setup(r => r.ObtenerAsync(10)).ReturnsAsync(Serie(10, "6", bloqueado: "S"));
            await AssertRechaza(Completa, MsgSerieFactura);
        }

        [Fact]
        public async Task Guardar_SerieFacturaDeOtroDocumento_Rechaza()
        {
            TodoValido();
            _repoSerie.Setup(r => r.ObtenerAsync(10)).ReturnsAsync(Serie(10, "3"));
            await AssertRechaza(Completa, MsgSerieFactura);
        }

        [Fact]
        public async Task Guardar_SerieFacturaConSubtipoDistintoDeGuiones_Rechaza()
        {
            TodoValido();
            _repoSerie.Setup(r => r.ObtenerAsync(10)).ReturnsAsync(Serie(10, "6", subTipo: "DB"));
            await AssertRechaza(Completa, MsgSerieFactura);
        }

        [Fact]
        public async Task Guardar_SeriePagoInexistente_Rechaza()
        {
            TodoValido();
            _repoSerie.Setup(r => r.ObtenerAsync(20)).ReturnsAsync((NumeracionDocumentoDet?)null);
            await AssertRechaza(Completa, MsgSeriePago);
        }

        [Fact]
        public async Task Guardar_SeriePagoManual_Rechaza()
        {
            TodoValido();
            _repoSerie.Setup(r => r.ObtenerAsync(20)).ReturnsAsync(Serie(20, "24", manual: "S"));
            await AssertRechaza(Completa, MsgSeriePago);
        }

        [Fact]
        public async Task Guardar_SeriePagoBloqueada_Rechaza()
        {
            TodoValido();
            _repoSerie.Setup(r => r.ObtenerAsync(20)).ReturnsAsync(Serie(20, "24", bloqueado: "S"));
            await AssertRechaza(Completa, MsgSeriePago);
        }

        [Fact]
        public async Task Guardar_SeriePagoDeOtroDocumento_Rechaza()
        {
            TodoValido();
            _repoSerie.Setup(r => r.ObtenerAsync(20)).ReturnsAsync(Serie(20, "6"));
            await AssertRechaza(Completa, MsgSeriePago);
        }

        [Fact]
        public async Task Guardar_AlmacenInexistente_Rechaza()
        {
            TodoValido();
            _repoAlmacen.Setup(r => r.ObtenerAsync("ALM1")).ReturnsAsync((Almacen?)null);
            await AssertRechaza(Completa, "El almacén no existe.");
        }

        [Fact]
        public async Task Guardar_ImpuestoInexistente_Rechaza()
        {
            TodoValido();
            _repoImpuesto.Setup(r => r.ObtenerAsync("IVA")).ReturnsAsync((Impuesto?)null);
            await AssertRechaza(Completa, "El impuesto no existe.");
        }

        [Fact]
        public async Task Guardar_ClienteInexistente_Rechaza()
        {
            TodoValido();
            _repoSocio.Setup(r => r.ObtenerAsync("C0001")).ReturnsAsync((SocioNegocio?)null);
            await AssertRechaza(Completa, "El cliente por defecto no existe o no es un cliente.");
        }

        [Fact]
        public async Task Guardar_ClienteQueEsProveedor_Rechaza()
        {
            TodoValido();
            _repoSocio.Setup(r => r.ObtenerAsync("C0001")).ReturnsAsync(new SocioNegocio { Codigo = "C0001", TipoSn = "P" });
            await AssertRechaza(Completa, "El cliente por defecto no existe o no es un cliente.");
        }

        [Fact]
        public async Task Guardar_FormaPagoInexistente_Rechaza()
        {
            TodoValido();
            _repoFormaPago.Setup(r => r.ObtenerAsync("EFE")).ReturnsAsync((FormaPago?)null);
            await AssertRechaza(Completa, "La forma de pago en efectivo no existe o está inactiva.");
        }

        [Fact]
        public async Task Guardar_FormaPagoInactiva_Rechaza()
        {
            TodoValido();
            _repoFormaPago.Setup(r => r.ObtenerAsync("EFE")).ReturnsAsync(new FormaPago { Codigo = "EFE", Nombre = "Efectivo", Activo = "N" });
            await AssertRechaza(Completa, "La forma de pago en efectivo no existe o está inactiva.");
        }

        [Fact]
        public async Task Guardar_MonedaInexistente_Rechaza()
        {
            TodoValido();
            _repoMoneda.Setup(r => r.ObtenerAsync("GTQ")).ReturnsAsync((Monedum?)null);
            await AssertRechaza(Completa, "La moneda no existe.");
        }

        // ---------- Guardar: persistencia ----------

        [Fact]
        public async Task Guardar_FilaExistente_ActualizaLosSieteValores()
        {
            TodoValido();
            _repoConfig.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new PosConfiguracion { Id = 1 });
            PosConfiguracion? guardada = null;
            _repoConfig.Setup(r => r.ActualizarAsync(1, It.IsAny<PosConfiguracion>()))
                .Callback<int, PosConfiguracion>((_, e) => guardada = e).ReturnsAsync(true);

            await _domain.GuardarConfiguracionAsync(Completa);

            Assert.NotNull(guardada);
            Assert.Equal(1, guardada!.Id);
            Assert.Equal(10, guardada.SerieFactura);
            Assert.Equal(20, guardada.SeriePago);
            Assert.Equal("ALM1", guardada.CodAlmacen);
            Assert.Equal("IVA", guardada.CodigoImpuesto);
            Assert.Equal("C0001", guardada.ClienteDefecto);
            Assert.Equal("EFE", guardada.FormaPagoEfectivo);
            Assert.Equal("GTQ", guardada.MonedaDoc);
            _repoConfig.Verify(r => r.InsertarAsync(It.IsAny<PosConfiguracion>()), Times.Never);
        }

        [Fact]
        public async Task Guardar_SinFila_LaCreaConIdUno()
        {
            TodoValido();
            _repoConfig.Setup(r => r.ObtenerAsync(1)).ReturnsAsync((PosConfiguracion?)null);
            PosConfiguracion? creada = null;
            _repoConfig.Setup(r => r.InsertarAsync(It.IsAny<PosConfiguracion>()))
                .Callback<PosConfiguracion>(e => creada = e).ReturnsAsync((PosConfiguracion e) => e);

            await _domain.GuardarConfiguracionAsync(Completa);

            Assert.NotNull(creada);
            Assert.Equal(1, creada!.Id);
            Assert.Equal(10, creada.SerieFactura);
            Assert.Equal(20, creada.SeriePago);
            Assert.Equal("ALM1", creada.CodAlmacen);
            Assert.Equal("IVA", creada.CodigoImpuesto);
            Assert.Equal("C0001", creada.ClienteDefecto);
            Assert.Equal("EFE", creada.FormaPagoEfectivo);
            Assert.Equal("GTQ", creada.MonedaDoc);
            _repoConfig.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<PosConfiguracion>()), Times.Never);
        }

        [Fact]
        public async Task Guardar_TodoNulo_EsValidoYNoConsultaReferencias()
        {
            _repoConfig.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new PosConfiguracion { Id = 1, SerieFactura = 10 });
            PosConfiguracion? guardada = null;
            _repoConfig.Setup(r => r.ActualizarAsync(1, It.IsAny<PosConfiguracion>()))
                .Callback<int, PosConfiguracion>((_, e) => guardada = e).ReturnsAsync(true);

            await _domain.GuardarConfiguracionAsync(new PosConfiguracionDato(null, null, null, null, null, null, null));

            Assert.NotNull(guardada);
            Assert.Null(guardada!.SerieFactura);
            Assert.Null(guardada.SeriePago);
            Assert.Null(guardada.CodAlmacen);
            Assert.Null(guardada.CodigoImpuesto);
            Assert.Null(guardada.ClienteDefecto);
            Assert.Null(guardada.FormaPagoEfectivo);
            Assert.Null(guardada.MonedaDoc);
            _repoSerie.Verify(r => r.ObtenerAsync(It.IsAny<int>()), Times.Never);
            _repoAlmacen.Verify(r => r.ObtenerAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Guardar_UnaReglaRota_NoPersisteNadaAunqueLasAnterioresPasen()
        {
            TodoValido();
            _repoConfig.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new PosConfiguracion { Id = 1 });
            _repoMoneda.Setup(r => r.ObtenerAsync("GTQ")).ReturnsAsync((Monedum?)null);

            await AssertRechaza(Completa, "La moneda no existe.");
        }

        // ---------- Métodos de la Tarea 6 ----------
    }
}
