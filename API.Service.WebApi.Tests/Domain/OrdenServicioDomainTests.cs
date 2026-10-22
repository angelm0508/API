using API.Domain.Core;
using API.Domain.Core.Inventario;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class OrdenServicioDomainTests
    {
        private readonly Mock<IRepositorioGenerico<OrdenServicio, int>> _repoOrden = new();
        private readonly Mock<IRepositorioGenerico<OrdenServicioDetalle, (int Entry, int NoLinea)>> _repoDetalle = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracion = new();
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocio = new();
        private readonly Mock<IRepositorioGenerico<Monedum, string>> _repoMoneda = new();
        private readonly Mock<IRepositorioGenerico<Motocicleta, int>> _repoMoto = new();
        private readonly Mock<IRepositorioGenerico<Mecanico, string>> _repoMecanico = new();
        private readonly Mock<IOrdenServicioLineaService> _lineas = new();
        private readonly Mock<IInventarioAsientoService> _asiento = new();
        private readonly Mock<IDocumentoBaseService> _documentoBase = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly List<string> _secuencia = new();
        private readonly OrdenServicioDomain _domain;

        public OrdenServicioDomainTests()
        {
            _domain = new OrdenServicioDomain(_repoOrden.Object, _repoDetalle.Object, _repoNumeracion.Object, _repoSocio.Object,
                _repoMoneda.Object, _repoMoto.Object, _repoMecanico.Object, _lineas.Object, _asiento.Object, _documentoBase.Object, _tx.Object);

            // El doble del ejecutor corre el Func directo (sin transacción ni save).
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).Returns<Func<Task<int>>>(f => f());
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(f => f());
            _repoOrden.Setup(r => r.InsertarAsync(It.IsAny<OrdenServicio>())).ReturnsAsync((OrdenServicio o) => { o.Entry = 99; return o; });
            _repoDetalle.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<OrdenServicioDetalle>())).Returns(Task.CompletedTask);

            // Valores por defecto válidos: cada prueba rompe solo lo que quiere probar.
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie());
            _repoSocio.Setup(r => r.ObtenerAsync("SN1")).ReturnsAsync(new SocioNegocio { Codigo = "SN1", Nombre = "Cliente Uno", TipoSn = "C", Activo = "S" });
            _repoMoneda.Setup(r => r.ObtenerAsync("QTZ")).ReturnsAsync(new Monedum { Codigo = "QTZ" });
            _repoMoto.Setup(r => r.ObtenerAsync(10)).ReturnsAsync(new Motocicleta { Entry = 10, CodigoSn = "SN1", Placa = "M123ABC", Activo = "S" });
            _repoMecanico.Setup(r => r.ObtenerAsync("M01")).ReturnsAsync(new Mecanico { Codigo = "M01", Nombre = "Juan", Activo = "S" });
        }

        private static NumeracionDocumentoDet CrearSerie(int? sig = 5, int? fin = null, string bloqueado = "N", string manual = "N", string codigoObj = "70") => new()
        {
            CodigoObj = codigoObj, Serie = 4, NombreSerie = "Primario", SigNumero = sig, FinNumero = fin,
            Bloqueado = bloqueado, Manual = manual, SubTipoDoc = "--", TipoSerie = "D"
        };

        private static OrdenServicio Orden(Action<OrdenServicio>? ajustar = null)
        {
            var o = new OrdenServicio { Serie = 4, CodigoSn = "SN1", MonedaDoc = "QTZ", MotoEntry = 10, CodMecanico = "M01" };
            ajustar?.Invoke(o);
            return o;
        }

        private OrdenServicio Existente(Action<OrdenServicio>? ajustar = null)
        {
            var o = new OrdenServicio
            {
                Entry = 50, NumDoc = 8, Serie = 4, CodigoSn = "SN1", NombreSn = "Cliente Uno", MonedaDoc = "QTZ", MotoEntry = 10,
                EstadoServicio = "R", Cancelado = "N", Comentario = "original"
            };
            ajustar?.Invoke(o);
            _repoOrden.Setup(r => r.ObtenerAsync(50)).ReturnsAsync(o);
            return o;
        }

        private async Task<string> MensajeAlInsertar(OrdenServicio orden, params OrdenServicioDetalle[] lineas)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(orden, lineas));
            return ex.Message;
        }

        // ---------------- InsertarAsync ----------------

        [Fact]
        public async Task InsertarAsync_Valida_NumeraGuardaEncabezadoYLineasYAsienta()
        {
            var serie = CrearSerie(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            var obj = Orden();
            var l1 = new OrdenServicioDetalle { CodArticulo = "ACE-1" };
            var l2 = new OrdenServicioDetalle { CodArticulo = "MO-1" };

            var entry = await _domain.InsertarAsync(obj, new[] { l1, l2 });

            Assert.Equal(99, entry);
            Assert.Equal("70", obj.TipoObjeto);
            Assert.Equal("N", obj.Cancelado);
            Assert.Equal("R", obj.EstadoServicio);
            Assert.Equal(5, obj.NumDoc);
            Assert.Equal(6, serie.SigNumero);                  // arrancaba en 5; debe haber avanzado
            Assert.Equal("Cliente Uno", obj.NombreSn);         // el servidor completa el nombre del socio
            Assert.NotNull(obj.FechaDoc);
            Assert.Equal(obj.FechaDoc, obj.FechaIngreso);
            Assert.Equal((99, 1), (l1.Entry, l1.NoLinea));
            Assert.Equal((99, 2), (l2.Entry, l2.NoLinea));
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(l1), Times.Once);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(l2), Times.Once);
            _lineas.Verify(l => l.PrepararAsync(It.Is<IList<OrdenServicioDetalle>>(x => x.Count == 2), 1), Times.Once);
            _lineas.Verify(l => l.RecalcularCabecera(obj, It.IsAny<IEnumerable<OrdenServicioDetalle>>()), Times.Once);
            _lineas.Verify(l => l.AsentarSalidaAsync(99, obj.FechaDoc!.Value, It.Is<IEnumerable<OrdenServicioDetalle>>(x => x.Count() == 2)), Times.Once);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_SinLineas_EsValido()
        {
            var entry = await _domain.InsertarAsync(Orden(), new List<OrdenServicioDetalle>());

            Assert.Equal(99, entry);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<OrdenServicioDetalle>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_IgnoraEstadoYCancelacionQueMandeElCliente()
        {
            var obj = Orden(o => { o.EstadoServicio = "E"; o.Cancelado = "S"; o.FechaCancelado = DateTime.Now; o.FechaEntrega = DateTime.Now; o.TipoObjeto = "6"; });

            await _domain.InsertarAsync(obj, new List<OrdenServicioDetalle>());

            Assert.Equal(("R", "N", "70"), (obj.EstadoServicio, obj.Cancelado, obj.TipoObjeto));
            Assert.Null(obj.FechaCancelado);
            Assert.Null(obj.FechaEntrega);
        }

        [Fact]
        public async Task InsertarAsync_ConservaNombreYFechasEnviadas()
        {
            var fecha = new DateTime(2026, 10, 20);
            var obj = Orden(o => { o.NombreSn = "Nombre del formulario"; o.FechaDoc = fecha; o.FechaIngreso = fecha.AddDays(-1); });

            await _domain.InsertarAsync(obj, new List<OrdenServicioDetalle>());

            Assert.Equal("Nombre del formulario", obj.NombreSn);
            Assert.Equal(fecha, obj.FechaDoc);
            Assert.Equal(fecha.AddDays(-1), obj.FechaIngreso);
        }

        [Fact]
        public async Task InsertarAsync_SerieInexistente_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync((NumeracionDocumentoDet?)null);

            Assert.Equal("La serie no existe.", await MensajeAlInsertar(Orden()));
        }

        [Fact]
        public async Task InsertarAsync_SerieDeOtroDocumento_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(codigoObj: "6"));

            Assert.Equal("La serie no corresponde a órdenes de servicio.", await MensajeAlInsertar(Orden()));
        }

        [Fact]
        public async Task InsertarAsync_SerieBloqueada_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(bloqueado: "S"));

            Assert.Equal("La serie está bloqueada y no se puede usar para registrar órdenes de servicio.", await MensajeAlInsertar(Orden()));
        }

        [Fact]
        public async Task InsertarAsync_SerieManualSinNumero_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(manual: "S"));

            Assert.Equal("El número de documento es requerido para series manuales.", await MensajeAlInsertar(Orden(o => o.NumDoc = 0)));
        }

        [Fact]
        public async Task InsertarAsync_SerieManualConNumero_ConservaElNumeroYNoAvanzaElConsecutivo()
        {
            var serie = CrearSerie(sig: 5, manual: "S");
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            var obj = Orden(o => o.NumDoc = 777);

            await _domain.InsertarAsync(obj, new List<OrdenServicioDetalle>());

            Assert.Equal(777, obj.NumDoc);
            Assert.Equal("S", obj.NumManual);
            Assert.Equal(5, serie.SigNumero);
        }

        [Fact]
        public async Task InsertarAsync_SerieAgotada_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(sig: 11, fin: 10));

            Assert.Equal("Se agotó la numeración disponible en esta serie.", await MensajeAlInsertar(Orden()));
        }

        [Fact]
        public async Task InsertarAsync_SinSocio_Lanza()
        {
            Assert.Equal("El socio de negocio es requerido.", await MensajeAlInsertar(Orden(o => o.CodigoSn = " ")));
        }

        [Fact]
        public async Task InsertarAsync_SocioInexistente_Lanza()
        {
            _repoSocio.Setup(r => r.ObtenerAsync("SN9")).ReturnsAsync((SocioNegocio?)null);

            Assert.Equal("El socio de negocio no existe.", await MensajeAlInsertar(Orden(o => o.CodigoSn = "SN9")));
        }

        [Fact]
        public async Task InsertarAsync_SocioQueNoEsCliente_Lanza()
        {
            _repoSocio.Setup(r => r.ObtenerAsync("PR1")).ReturnsAsync(new SocioNegocio { Codigo = "PR1", TipoSn = "P", Activo = "S" });

            Assert.Equal("El socio de negocio debe ser un cliente.", await MensajeAlInsertar(Orden(o => o.CodigoSn = "PR1")));
        }

        [Fact]
        public async Task InsertarAsync_SocioInactivo_Lanza()
        {
            _repoSocio.Setup(r => r.ObtenerAsync("SN2")).ReturnsAsync(new SocioNegocio { Codigo = "SN2", TipoSn = "C", Activo = "N" });

            Assert.Equal("El socio de negocio está inactivo.", await MensajeAlInsertar(Orden(o => o.CodigoSn = "SN2")));
        }

        [Fact]
        public async Task InsertarAsync_SinMoneda_Lanza()
        {
            Assert.Equal("La moneda es requerida.", await MensajeAlInsertar(Orden(o => o.MonedaDoc = "")));
        }

        [Fact]
        public async Task InsertarAsync_MonedaInexistente_Lanza()
        {
            _repoMoneda.Setup(r => r.ObtenerAsync("XXX")).ReturnsAsync((Monedum?)null);

            Assert.Equal("La moneda no existe.", await MensajeAlInsertar(Orden(o => o.MonedaDoc = "XXX")));
        }

        [Fact]
        public async Task InsertarAsync_SinMoto_Lanza()
        {
            Assert.Equal("La motocicleta es requerida.", await MensajeAlInsertar(Orden(o => o.MotoEntry = 0)));
        }

        [Fact]
        public async Task InsertarAsync_MotoInexistente_Lanza()
        {
            _repoMoto.Setup(r => r.ObtenerAsync(77)).ReturnsAsync((Motocicleta?)null);

            Assert.Equal("La motocicleta no existe.", await MensajeAlInsertar(Orden(o => o.MotoEntry = 77)));
        }

        [Fact]
        public async Task InsertarAsync_MotoInactiva_Lanza()
        {
            _repoMoto.Setup(r => r.ObtenerAsync(11)).ReturnsAsync(new Motocicleta { Entry = 11, CodigoSn = "SN1", Placa = "M1", Activo = "N" });

            Assert.Equal("La motocicleta está inactiva.", await MensajeAlInsertar(Orden(o => o.MotoEntry = 11)));
        }

        [Fact]
        public async Task InsertarAsync_MotoDeOtroCliente_Lanza()
        {
            _repoMoto.Setup(r => r.ObtenerAsync(12)).ReturnsAsync(new Motocicleta { Entry = 12, CodigoSn = "SN7", Placa = "M2", Activo = "S" });

            Assert.Equal("La motocicleta no pertenece a este cliente.", await MensajeAlInsertar(Orden(o => o.MotoEntry = 12)));
        }

        [Fact]
        public async Task InsertarAsync_MecanicoInexistente_Lanza()
        {
            _repoMecanico.Setup(r => r.ObtenerAsync("M99")).ReturnsAsync((Mecanico?)null);

            Assert.Equal("El mecánico no existe.", await MensajeAlInsertar(Orden(o => o.CodMecanico = "M99")));
        }

        [Fact]
        public async Task InsertarAsync_MecanicoInactivo_Lanza()
        {
            _repoMecanico.Setup(r => r.ObtenerAsync("M02")).ReturnsAsync(new Mecanico { Codigo = "M02", Nombre = "Pedro", Activo = "N" });

            Assert.Equal("El mecánico está inactivo.", await MensajeAlInsertar(Orden(o => o.CodMecanico = "M02")));
        }

        [Fact]
        public async Task InsertarAsync_SinMecanico_EsValido()
        {
            await _domain.InsertarAsync(Orden(o => o.CodMecanico = null), new List<OrdenServicioDetalle>());
        }

        [Fact]
        public async Task InsertarAsync_KilometrajeNegativo_Lanza()
        {
            Assert.Equal("El kilometraje de ingreso no puede ser negativo.", await MensajeAlInsertar(Orden(o => o.KmIngreso = -5)));
        }

        [Fact]
        public async Task InsertarAsync_LineaInvalida_NoNumeraNiGuarda()
        {
            var serie = CrearSerie(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _lineas.Setup(l => l.PrepararAsync(It.IsAny<IList<OrdenServicioDetalle>>(), It.IsAny<int>()))
                .ThrowsAsync(new Exception("Línea 1: la cantidad debe ser mayor que 0."));

            Assert.Equal("Línea 1: la cantidad debe ser mayor que 0.", await MensajeAlInsertar(Orden(), new OrdenServicioDetalle { CodArticulo = "ACE-1" }));

            Assert.Equal(5, serie.SigNumero);   // el consecutivo no avanza ante un error de validación
            _repoOrden.Verify(r => r.InsertarAsync(It.IsAny<OrdenServicio>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_StockInsuficiente_PropagaLaExcepcion()
        {
            _lineas.Setup(l => l.AsentarSalidaAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<IEnumerable<OrdenServicioDetalle>>()))
                .ThrowsAsync(new StockInsuficienteException("ACE-1", "01", 1m, 5m));

            await Assert.ThrowsAsync<StockInsuficienteException>(() => _domain.InsertarAsync(Orden(), new[] { new OrdenServicioDetalle { CodArticulo = "ACE-1" } }));
        }

        // ---------------- ActualizarAsync ----------------

        [Fact]
        public async Task ActualizarAsync_Inexistente_DevuelveFalso()
        {
            _repoOrden.Setup(r => r.ObtenerAsync(99)).ReturnsAsync((OrdenServicio?)null);

            Assert.False(await _domain.ActualizarAsync(99, Orden()));
        }

        [Fact]
        public async Task ActualizarAsync_Cancelada_Lanza()
        {
            Existente(o => o.Cancelado = "S");

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(50, Orden()));
            Assert.Equal("La orden de servicio está cancelada y no se puede modificar.", ex.Message);
        }

        [Fact]
        public async Task ActualizarAsync_Entregada_SoloCambiaElComentario()
        {
            var existente = Existente(o => o.EstadoServicio = "E");

            var ok = await _domain.ActualizarAsync(50, Orden(o => { o.Comentario = "nuevo"; o.Diagnostico = "no debe cambiar"; o.CodigoSn = "OTRO"; }));

            Assert.True(ok);
            Assert.Equal("nuevo", existente.Comentario);
            Assert.Null(existente.Diagnostico);
            Assert.Equal("SN1", existente.CodigoSn);
            _repoSocio.Verify(r => r.ObtenerAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_Edicion_CopiaLosCamposEditables()
        {
            var existente = Existente(o => o.EstadoServicio = "P");
            var fecha = new DateTime(2026, 10, 25);
            var obj = Orden(o =>
            {
                o.Direccion = "Zona 1"; o.CodMecanico = "M01"; o.KmIngreso = 12000; o.FechaPromesa = fecha;
                o.FallaReportada = "No enciende"; o.Diagnostico = "Bujía"; o.Observaciones = "Cliente espera"; o.Comentario = "editado";
            });

            Assert.True(await _domain.ActualizarAsync(50, obj));

            Assert.Equal(("Zona 1", "M01", 12000, fecha), (existente.Direccion, existente.CodMecanico, existente.KmIngreso, existente.FechaPromesa));
            Assert.Equal(("No enciende", "Bujía", "Cliente espera", "editado"),
                (existente.FallaReportada, existente.Diagnostico, existente.Observaciones, existente.Comentario));
            Assert.Equal("P", existente.EstadoServicio);   // el estado no se cambia por aquí
            Assert.Equal(8, existente.NumDoc);
            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_CambiarDeCliente_ValidaQueNoTengaFacturas()
        {
            Existente();
            _repoSocio.Setup(r => r.ObtenerAsync("SN3")).ReturnsAsync(new SocioNegocio { Codigo = "SN3", Nombre = "Cliente Tres", TipoSn = "C", Activo = "S" });
            _repoMoto.Setup(r => r.ObtenerAsync(13)).ReturnsAsync(new Motocicleta { Entry = 13, CodigoSn = "SN3", Placa = "M3", Activo = "S" });

            Assert.True(await _domain.ActualizarAsync(50, Orden(o => { o.CodigoSn = "SN3"; o.MotoEntry = 13; })));

            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(TipoDocumentoVenta.OrdenServicio, 50, "cambiar el cliente"), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_CambiarDeClienteConFacturas_LanzaYNoCambia()
        {
            var existente = Existente();
            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(TipoDocumentoVenta.OrdenServicio, 50, "cambiar el cliente"))
                .ThrowsAsync(new Exception("No se puede cambiar el cliente: lo usan Factura 2. Cancele primero esos documentos."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(50, Orden(o => o.CodigoSn = "SN3")));

            Assert.Equal("No se puede cambiar el cliente: lo usan Factura 2. Cancele primero esos documentos.", ex.Message);
            Assert.Equal("SN1", existente.CodigoSn);
        }

        [Fact]
        public async Task ActualizarAsync_MotoDeOtroCliente_Lanza()
        {
            Existente();
            _repoMoto.Setup(r => r.ObtenerAsync(12)).ReturnsAsync(new Motocicleta { Entry = 12, CodigoSn = "SN7", Placa = "M2", Activo = "S" });

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(50, Orden(o => o.MotoEntry = 12)));
            Assert.Equal("La motocicleta no pertenece a este cliente.", ex.Message);
        }

        // ---------------- CambiarEstadoAsync ----------------

        [Theory]
        [InlineData("R", "P")]
        [InlineData("P", "T")]
        [InlineData("R", "T")]      // se puede saltar hacia adelante
        [InlineData("T", "E")]
        public async Task CambiarEstadoAsync_Avanza(string desde, string hacia)
        {
            var existente = Existente(o => o.EstadoServicio = desde);

            Assert.True(await _domain.CambiarEstadoAsync(50, hacia));

            Assert.Equal(hacia, existente.EstadoServicio);
        }

        [Fact]
        public async Task CambiarEstadoAsync_AEntregada_RegistraLaFechaDeEntrega()
        {
            var existente = Existente(o => o.EstadoServicio = "T");

            await _domain.CambiarEstadoAsync(50, "E");

            Assert.NotNull(existente.FechaEntrega);
        }

        [Fact]
        public async Task CambiarEstadoAsync_ATerminada_NoRegistraFechaDeEntrega()
        {
            var existente = Existente(o => o.EstadoServicio = "P");

            await _domain.CambiarEstadoAsync(50, "T");

            Assert.Null(existente.FechaEntrega);
        }

        [Theory]
        [InlineData("T", "P", "No se puede pasar de Terminada a En proceso: la orden solo avanza.")]
        [InlineData("E", "T", "No se puede pasar de Entregada a Terminada: la orden solo avanza.")]
        [InlineData("P", "R", "No se puede pasar de En proceso a Recibida: la orden solo avanza.")]
        public async Task CambiarEstadoAsync_Retroceder_Lanza(string desde, string hacia, string mensaje)
        {
            Existente(o => o.EstadoServicio = desde);

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.CambiarEstadoAsync(50, hacia));
            Assert.Equal(mensaje, ex.Message);
        }

        [Fact]
        public async Task CambiarEstadoAsync_MismoEstado_Lanza()
        {
            Existente(o => o.EstadoServicio = "P");

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.CambiarEstadoAsync(50, "P"));
            Assert.Equal("La orden ya está en estado En proceso.", ex.Message);
        }

        [Fact]
        public async Task CambiarEstadoAsync_EstadoInvalido_Lanza()
        {
            Existente();

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.CambiarEstadoAsync(50, "X"));
            Assert.Equal("El estado de la orden no es válido.", ex.Message);
        }

        [Fact]
        public async Task CambiarEstadoAsync_Cancelada_Lanza()
        {
            Existente(o => o.Cancelado = "S");

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.CambiarEstadoAsync(50, "P"));
            Assert.Equal("La orden de servicio está cancelada y no se puede modificar.", ex.Message);
        }

        [Fact]
        public async Task CambiarEstadoAsync_Inexistente_DevuelveFalso()
        {
            _repoOrden.Setup(r => r.ObtenerAsync(99)).ReturnsAsync((OrdenServicio?)null);

            Assert.False(await _domain.CambiarEstadoAsync(99, "P"));
        }

        // ---------------- CancelarAsync ----------------

        [Fact]
        public async Task CancelarAsync_ValidaDerivadosLuegoRevierteElInventarioYCancela()
        {
            var existente = Existente(o => o.EstadoServicio = "T");
            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(TipoDocumentoVenta.OrdenServicio, 50, "cancelar el documento"))
                .Callback(() => _secuencia.Add("validar")).Returns(Task.CompletedTask);
            _asiento.Setup(a => a.RevertirAsync("70", 50)).Callback(() => _secuencia.Add("revertir")).Returns(Task.CompletedTask);

            Assert.True(await _domain.CancelarAsync(50));

            Assert.Equal(new[] { "validar", "revertir" }, _secuencia);
            Assert.Equal("S", existente.Cancelado);
            Assert.NotNull(existente.FechaCancelado);
            Assert.Equal("original", existente.Comentario);
        }

        [Fact]
        public async Task CancelarAsync_ConFacturasActivas_LanzaYNoRevierte()
        {
            var existente = Existente();
            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(TipoDocumentoVenta.OrdenServicio, 50, "cancelar el documento"))
                .ThrowsAsync(new Exception("No se puede cancelar el documento: lo usan Factura 2. Cancele primero esos documentos."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.CancelarAsync(50));

            Assert.Equal("No se puede cancelar el documento: lo usan Factura 2. Cancele primero esos documentos.", ex.Message);
            _asiento.Verify(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
            Assert.Equal("N", existente.Cancelado);
        }

        [Fact]
        public async Task CancelarAsync_Entregada_Lanza()
        {
            Existente(o => o.EstadoServicio = "E");

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.CancelarAsync(50));
            Assert.Equal("Una orden entregada no se puede cancelar.", ex.Message);
        }

        [Fact]
        public async Task CancelarAsync_YaCancelada_Lanza()
        {
            Existente(o => o.Cancelado = "S");

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.CancelarAsync(50));
            Assert.Equal("La orden de servicio ya está cancelada.", ex.Message);
        }

        [Fact]
        public async Task CancelarAsync_Inexistente_DevuelveFalso()
        {
            _repoOrden.Setup(r => r.ObtenerAsync(99)).ReturnsAsync((OrdenServicio?)null);

            Assert.False(await _domain.CancelarAsync(99));
        }

        // ---------------- Consultas ----------------

        [Fact]
        public async Task ObtenerLineasAsync_DevuelveLasLineasDeLaOrdenOrdenadasPorNumero()
        {
            var todas = new List<OrdenServicioDetalle>
            {
                new() { Entry = 50, NoLinea = 2 },
                new() { Entry = 51, NoLinea = 1 },
                new() { Entry = 50, NoLinea = 1 },
            };
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(todas.AsAsyncQueryable());

            var lineas = (await _domain.ObtenerLineasAsync(50)).Select(l => l.NoLinea).ToList();

            Assert.Equal(new[] { 1, 2 }, lineas);
        }
    }
}
