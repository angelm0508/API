using System.Data;
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class PagoEfectuadoDomainTests
    {
        private readonly Mock<IRepositorioGenerico<PagoEfectuado, int>> _repoPago = new();
        private readonly Mock<IRepositorioGenerico<PagoEfectuadoFactura, (int Entry, int NoLinea)>> _repoPagoFactura = new();
        private readonly Mock<IRepositorioGenerico<PagoEfectuadoMedio, (int Entry, int NoLinea)>> _repoMedio = new();
        private readonly Mock<IRepositorioGenerico<FormaPago, string>> _repoForma = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracion = new();
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocio = new();
        private readonly Mock<IRepositorioGenerico<FacturaCompra, int>> _repoFactura = new();
        private readonly Mock<ISaldoFacturaCompraService> _saldos = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly PagoEfectuadoDomain _domain;

        public PagoEfectuadoDomainTests()
        {
            _domain = new PagoEfectuadoDomain(
                _repoPago.Object, _repoPagoFactura.Object, _repoMedio.Object, _repoForma.Object,
                _repoNumeracion.Object, _repoSocio.Object, _repoFactura.Object, _saldos.Object, _tx.Object);

            // El doble del ejecutor corre el Func directo (sin transaccion ni save).
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>(), It.IsAny<IsolationLevel>()))
                .Returns<Func<Task<int>>, IsolationLevel>((f, _) => f());
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(f => f());
            _repoPago.Setup(r => r.InsertarAsync(It.IsAny<PagoEfectuado>()))
                .ReturnsAsync((PagoEfectuado p) => { p.Entry = 99; return p; });
            _repoPagoFactura.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<PagoEfectuadoFactura>())).Returns(Task.CompletedTask);
            _repoMedio.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<PagoEfectuadoMedio>())).Returns(Task.CompletedTask);
            _repoPagoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PagoEfectuadoFactura>().AsAsyncQueryable());
            _repoMedio.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PagoEfectuadoMedio>().AsAsyncQueryable());

            // Valores por defecto validos: cada test rompe solo lo que quiere probar.
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie());
            _repoSocio.Setup(r => r.ObtenerAsync("SN1")).ReturnsAsync(new SocioNegocio { Codigo = "SN1", Nombre = "Proveedor SN1", TipoSn = "P", Activo = "S" });
            _repoFactura.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(Doc(1, 10));
            _repoFactura.Setup(r => r.ObtenerAsync(2)).ReturnsAsync(Doc(2, 20, "ND"));
            // Relectura transaccional del estado (Cancelado) de los documentos: coherente con ObtenerAsync.
            _repoFactura.Setup(r => r.ObtenerTodoAsync())
                .ReturnsAsync(() => new List<FacturaCompra> { Doc(1, 10), Doc(2, 20, "ND") }.AsAsyncQueryable());
            _repoForma.Setup(r => r.ObtenerAsync("EFE")).ReturnsAsync(Forma("EFE", "Efectivo", "N"));
            _repoForma.Setup(r => r.ObtenerAsync("CHQ")).ReturnsAsync(Forma("CHQ", "Cheque", "C"));
            _repoForma.Setup(r => r.ObtenerAsync("TRF")).ReturnsAsync(Forma("TRF", "Transferencia", "T"));
            _repoForma.Setup(r => r.ObtenerAsync("TJT")).ReturnsAsync(Forma("TJT", "Tarjeta", "J"));
            SaldoDisponible(100m);
        }

        private void SaldoDisponible(decimal saldo) =>
            _saldos.Setup(s => s.ObtenerAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync((IEnumerable<int> ids) => (IReadOnlyDictionary<int, SaldoFacturaCompra>)ids.Distinct()
                    .ToDictionary(i => i, i => new SaldoFacturaCompra(i, 100m, 0m, 100m - saldo, saldo, "Pendiente")));

        private static NumeracionDocumentoDet CrearSerie(int? sig = 5, int? fin = null, string bloqueado = "N", string manual = "N", string? codigoObj = null) => new()
        {
            CodigoObj = codigoObj ?? "46", Serie = 4, NombreSerie = "Primario",
            SigNumero = sig, FinNumero = fin, Bloqueado = bloqueado, Manual = manual,
            SubTipoDoc = "--", TipoSerie = "D"
        };

        private static FacturaCompra Doc(int entry, int numDoc, string subTipo = "--", string proveedor = "SN1", string cancelado = "N") => new()
        {
            Entry = entry, NumDoc = numDoc, CodigoSn = proveedor, Cancelado = cancelado, DocSubType = subTipo,
            FechaDoc = new DateTime(2026, 10, 1), TotalDoc = 100m
        };

        private static FormaPago Forma(string codigo, string nombre, string tipo, string activo = "S") =>
            new() { Codigo = codigo, Nombre = nombre, TipoDato = tipo, Activo = activo };

        private static PagoEfectuado Pago(Action<PagoEfectuado>? ajustar = null)
        {
            var p = new PagoEfectuado { Serie = 4, CodigoSn = "SN1", FechaDoc = new DateTime(2026, 10, 23) };
            ajustar?.Invoke(p);
            return p;
        }

        private static PagoEfectuadoFactura Aplicar(int facturaEntry, decimal monto) => new() { FacturaCompraEntry = facturaEntry, MontoAplicado = monto };

        private static PagoEfectuadoMedio Medio(string forma, decimal monto, string? referencia = null, string? banco = null, DateTime? fechaCheque = null) =>
            new() { CodFormaPago = forma, Monto = monto, Referencia = referencia, Banco = banco, FechaCheque = fechaCheque };

        private async Task<string> MensajeAlInsertar(PagoEfectuado pago, PagoEfectuadoFactura[] facturas, PagoEfectuadoMedio[] medios)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(pago, facturas, medios));
            return ex.Message;
        }

        // Escenario valido minimo: factura 1 por 100 pagada en efectivo.
        private Task<int> InsertarValido(PagoEfectuado? pago = null) =>
            _domain.InsertarAsync(pago ?? Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) });

        // ---------------- InsertarAsync: camino feliz ----------------

        [Fact]
        public async Task InsertarAsync_Valido_NumeraGuardaCabeceraFacturasYMedios()
        {
            var serie = CrearSerie(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            var pago = Pago();

            var entry = await InsertarValido(pago);

            Assert.Equal(99, entry);
            Assert.Equal("46", pago.TipoObjeto);
            Assert.Equal(5, pago.NumDoc);
            Assert.Equal(6, serie.SigNumero);
            Assert.Equal("N", pago.NumManual);
            Assert.Equal("N", pago.Cancelado);
            Assert.Equal(100m, pago.TotalPago);
            Assert.Equal("Proveedor SN1", pago.NombreSn);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>(), IsolationLevel.Serializable), Times.Once);
            _repoPagoFactura.Verify(r => r.AgregarSinGuardarAsync(It.Is<PagoEfectuadoFactura>(l => l.Entry == 99 && l.NoLinea == 1 && l.FacturaCompraEntry == 1 && l.MontoAplicado == 100m)), Times.Once);
            _repoMedio.Verify(r => r.AgregarSinGuardarAsync(It.Is<PagoEfectuadoMedio>(m => m.Entry == 99 && m.NoLinea == 1 && m.CodFormaPago == "EFE" && m.Monto == 100m)), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_VariosDocumentosYMedios_NoLineasConsecutivosYTotalEsLaSuma()
        {
            var pago = Pago();

            await _domain.InsertarAsync(pago,
                new[] { Aplicar(1, 60m), Aplicar(2, 40m) },
                new[] { Medio("EFE", 70m), Medio("CHQ", 30m, "00123", "Banco Industrial") });

            Assert.Equal(100m, pago.TotalPago);
            _repoPagoFactura.Verify(r => r.AgregarSinGuardarAsync(It.Is<PagoEfectuadoFactura>(l => l.NoLinea == 1 && l.FacturaCompraEntry == 1)), Times.Once);
            _repoPagoFactura.Verify(r => r.AgregarSinGuardarAsync(It.Is<PagoEfectuadoFactura>(l => l.NoLinea == 2 && l.FacturaCompraEntry == 2)), Times.Once);
            _repoMedio.Verify(r => r.AgregarSinGuardarAsync(It.Is<PagoEfectuadoMedio>(m => m.NoLinea == 2 && m.CodFormaPago == "CHQ")), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_ForzaCanceladoNoYFechaCanceladoNulo()
        {
            var pago = Pago(p => { p.Cancelado = "S"; p.FechaCancelado = DateTime.Now; p.TipoObjeto = "99"; });

            await InsertarValido(pago);

            Assert.Equal("N", pago.Cancelado);
            Assert.Null(pago.FechaCancelado);
            Assert.Equal("46", pago.TipoObjeto);
        }

        [Fact]
        public async Task InsertarAsync_SinFecha_UsaLaFechaActual()
        {
            var pago = Pago(p => p.FechaDoc = null);

            await InsertarValido(pago);

            Assert.NotNull(pago.FechaDoc);
        }

        [Fact]
        public async Task InsertarAsync_ProveedorInactivo_SePermite()
        {
            _repoSocio.Setup(r => r.ObtenerAsync("SN1")).ReturnsAsync(new SocioNegocio { Codigo = "SN1", Nombre = "Proveedor SN1", TipoSn = "P", Activo = "N" });

            Assert.Equal(99, await InsertarValido());
        }

        [Fact]
        public async Task InsertarAsync_NotaDeDebito_SePuedePagar()
        {
            var entry = await _domain.InsertarAsync(Pago(), new[] { Aplicar(2, 50m) }, new[] { Medio("EFE", 50m) });

            Assert.Equal(99, entry);
        }

        [Fact]
        public async Task InsertarAsync_SerieManual_UsaElNumeroIndicadoYNoAvanzaElConsecutivo()
        {
            var serie = CrearSerie(sig: 5, manual: "S");
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            var pago = Pago(p => p.NumDoc = 77);

            await InsertarValido(pago);

            Assert.Equal("S", pago.NumManual);
            Assert.Equal(77, pago.NumDoc);
            Assert.Equal(5, serie.SigNumero);
        }

        // ---------------- InsertarAsync: serie ----------------

        [Fact]
        public async Task InsertarAsync_SerieInexistente_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync((NumeracionDocumentoDet?)null);

            Assert.Equal("La serie no existe.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_SerieDeOtroObjeto_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(codigoObj: "6"));

            Assert.Equal("La serie no corresponde a pagos efectuados.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_SerieBloqueada_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(bloqueado: "S"));

            Assert.Equal("La serie está bloqueada y no se puede usar para registrar pagos efectuados.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_SerieManualSinNumero_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(manual: "S"));

            Assert.Equal("El número de documento es requerido para series manuales.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_SerieSinSiguienteNumero_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(sig: null));

            Assert.Equal("La serie no tiene configurado el número siguiente.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_SerieAgotada_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(sig: 11, fin: 10));

            Assert.Equal("Se agotó la numeración disponible en esta serie.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        // ---------------- InsertarAsync: proveedor ----------------

        [Fact]
        public async Task InsertarAsync_SinProveedor_Lanza()
        {
            Assert.Equal("El socio de negocio es requerido.", await MensajeAlInsertar(Pago(p => p.CodigoSn = " "), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_ProveedorInexistente_Lanza()
        {
            _repoSocio.Setup(r => r.ObtenerAsync("SN9")).ReturnsAsync((SocioNegocio?)null);

            Assert.Equal("El socio de negocio no existe.", await MensajeAlInsertar(Pago(p => p.CodigoSn = "SN9"), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_SocioQueNoEsProveedor_Lanza()
        {
            _repoSocio.Setup(r => r.ObtenerAsync("SN1")).ReturnsAsync(new SocioNegocio { Codigo = "SN1", Nombre = "Cliente", TipoSn = "C", Activo = "S" });

            Assert.Equal("El socio de negocio debe ser un proveedor.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        // ---------------- InsertarAsync: documentos ----------------

        [Fact]
        public async Task InsertarAsync_SinDocumentos_Lanza()
        {
            Assert.Equal("Indique al menos un documento a pagar.", await MensajeAlInsertar(Pago(), Array.Empty<PagoEfectuadoFactura>(), new[] { Medio("EFE", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_DocumentoRepetido_Lanza()
        {
            Assert.Equal("El documento 1 está repetido en el pago.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 50m), Aplicar(1, 50m) }, new[] { Medio("EFE", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_DocumentoInexistente_Lanza()
        {
            _repoFactura.Setup(r => r.ObtenerAsync(9)).ReturnsAsync((FacturaCompra?)null);

            Assert.Equal("El documento 9 no existe.", await MensajeAlInsertar(Pago(), new[] { Aplicar(9, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_DocumentoDeOtroProveedor_Lanza()
        {
            _repoFactura.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(Doc(1, 10, proveedor: "SN2"));

            Assert.Equal("Factura de compra 10 pertenece a otro socio de negocio.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_DocumentoCancelado_Lanza()
        {
            _repoFactura.Setup(r => r.ObtenerAsync(2)).ReturnsAsync(Doc(2, 20, "ND", cancelado: "S"));

            Assert.Equal("Nota de débito de proveedor 20 está cancelada.", await MensajeAlInsertar(Pago(), new[] { Aplicar(2, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task InsertarAsync_MontoAplicadoNoPositivo_Lanza(int monto)
        {
            Assert.Equal("El monto a pagar de Factura de compra 10 debe ser mayor que 0.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, monto) }, new[] { Medio("EFE", 10m) }));
        }

        [Fact]
        public async Task InsertarAsync_MontoAplicadoConMasDeDosDecimales_Lanza()
        {
            Assert.Equal("El monto a pagar de Factura de compra 10 no puede tener más de 2 decimales.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 10.005m) }, new[] { Medio("EFE", 10.005m) }));
        }

        [Fact]
        public async Task InsertarAsync_MontoMayorQueElSaldo_LanzaDentroDeLaTransaccionSerializable()
        {
            SaldoDisponible(60m);

            var mensaje = await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) });

            Assert.Equal("El monto a pagar de Factura de compra 10 (100.00) excede su saldo (60.00).", mensaje);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>(), IsolationLevel.Serializable), Times.Once);
            _repoPago.Verify(r => r.InsertarAsync(It.IsAny<PagoEfectuado>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_DocumentoCanceladoAntesDeLaTransaccion_SeRechazaEnLaRelecturaSerializable()
        {
            // ObtenerAsync (antes de la tx) ve la ND vigente; otro usuario la cancela antes de que el pago entre a su
            // transaccion: la relectura dentro de la tx Serializable debe verla cancelada.
            var dentroDeLaTx = false;
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>(), IsolationLevel.Serializable))
                .Returns<Func<Task<int>>, IsolationLevel>(async (f, _) =>
                {
                    dentroDeLaTx = true;
                    try { return await f(); } finally { dentroDeLaTx = false; }
                });
            var leidoDentro = false;
            _repoFactura.Setup(r => r.ObtenerTodoAsync())
                .ReturnsAsync(() =>
                {
                    leidoDentro |= dentroDeLaTx;
                    return new List<FacturaCompra> { Doc(1, 10), Doc(2, 20, "ND", cancelado: "S") }.AsAsyncQueryable();
                });

            var mensaje = await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 60m), Aplicar(2, 40m) }, new[] { Medio("EFE", 100m) });

            Assert.Equal("Nota de débito de proveedor 20 está cancelada.", mensaje);
            Assert.True(leidoDentro);
            _repoPago.Verify(r => r.InsertarAsync(It.IsAny<PagoEfectuado>()), Times.Never);
            _repoPagoFactura.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<PagoEfectuadoFactura>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_DocumentosVigentesEnLaRelectura_Pasa()
        {
            var entry = await _domain.InsertarAsync(Pago(), new[] { Aplicar(1, 60m), Aplicar(2, 40m) }, new[] { Medio("EFE", 100m) });

            Assert.Equal(99, entry);
            _repoFactura.Verify(r => r.ObtenerTodoAsync(), Times.Once);
            _repoPago.Verify(r => r.InsertarAsync(It.IsAny<PagoEfectuado>()), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_MontoIgualAlSaldo_Pasa()
        {
            SaldoDisponible(100m);

            Assert.Equal(99, await InsertarValido());
        }

        // ---------------- InsertarAsync: formas de pago ----------------

        [Fact]
        public async Task InsertarAsync_SinFormasDePago_InvocaElValidadorDeMedios()
        {
            Assert.Equal("Indique al menos una forma de pago.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, Array.Empty<PagoEfectuadoMedio>()));
        }

        [Fact]
        public async Task InsertarAsync_ChequeSinBanco_InvocaElValidadorDeMedios()
        {
            Assert.Equal("La forma de pago Cheque requiere el banco.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("CHQ", 100m, referencia: "123") }));
        }

        // ---------------- InsertarAsync: cuadre ----------------

        [Fact]
        public async Task InsertarAsync_TotalAplicadoDistintoDeLosMedios_Lanza()
        {
            Assert.Equal("El total aplicado a los documentos (100.00) no coincide con el total de las formas de pago (90.00).",
                await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 90m) }));
        }

        [Fact]
        public async Task InsertarAsync_ErrorDeValidacion_NoAvanzaElConsecutivoNiAbreTransaccion()
        {
            var serie = CrearSerie(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 90m) }));

            Assert.Equal(5, serie.SigNumero);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>(), It.IsAny<IsolationLevel>()), Times.Never);
        }

        // ---------------- CancelarAsync ----------------

        [Fact]
        public async Task CancelarAsync_MarcaCanceladoConFechaDentroDeTransaccion()
        {
            var pago = new PagoEfectuado { Entry = 7, Cancelado = "N" };
            _repoPago.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(pago);

            Assert.True(await _domain.CancelarAsync(7));

            Assert.Equal("S", pago.Cancelado);
            Assert.NotNull(pago.FechaCancelado);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>()), Times.Once);
        }

        [Fact]
        public async Task CancelarAsync_Inexistente_DevuelveFalse()
        {
            _repoPago.Setup(r => r.ObtenerAsync(7)).ReturnsAsync((PagoEfectuado?)null);

            Assert.False(await _domain.CancelarAsync(7));
        }

        [Fact]
        public async Task CancelarAsync_YaCancelado_Lanza()
        {
            _repoPago.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new PagoEfectuado { Entry = 7, Cancelado = "S" });

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.CancelarAsync(7));

            Assert.Equal("El pago ya está cancelado.", ex.Message);
        }

        // ---------------- consultas ----------------

        [Fact]
        public async Task ObtenerTodosAsync_DelegaEnElRepositorioDePagos()
        {
            var pagos = new List<PagoEfectuado> { new() { Entry = 1 }, new() { Entry = 2 } }.AsAsyncQueryable();
            _repoPago.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(pagos);

            Assert.Equal(2, (await _domain.ObtenerTodosAsync()).Count());
        }

        [Fact]
        public async Task ObtenerFacturasAsync_DevuelveLasLineasDelPagoOrdenadas()
        {
            _repoPagoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PagoEfectuadoFactura>
            {
                new() { Entry = 7, NoLinea = 2, FacturaCompraEntry = 2, MontoAplicado = 40m },
                new() { Entry = 7, NoLinea = 1, FacturaCompraEntry = 1, MontoAplicado = 60m },
                new() { Entry = 8, NoLinea = 1, FacturaCompraEntry = 1, MontoAplicado = 5m }
            }.AsAsyncQueryable());

            var lineas = (await _domain.ObtenerFacturasAsync(7)).ToList();

            Assert.Equal(new[] { 1, 2 }, lineas.Select(l => l.NoLinea).ToArray());
        }

        [Fact]
        public async Task ObtenerMediosAsync_DevuelveLosMediosDelPagoOrdenados()
        {
            _repoMedio.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PagoEfectuadoMedio>
            {
                new() { Entry = 7, NoLinea = 2, CodFormaPago = "CHQ", Monto = 30m },
                new() { Entry = 7, NoLinea = 1, CodFormaPago = "EFE", Monto = 70m },
                new() { Entry = 8, NoLinea = 1, CodFormaPago = "EFE", Monto = 5m }
            }.AsAsyncQueryable());

            var medios = (await _domain.ObtenerMediosAsync(7)).ToList();

            Assert.Equal(new[] { "EFE", "CHQ" }, medios.Select(m => m.CodFormaPago).ToArray());
        }

        [Fact]
        public async Task ObtenerAbiertosAsync_SoloDelProveedorNoCanceladosYConSaldo_OrdenadosPorFecha()
        {
            var docs = new List<FacturaCompra>
            {
                Doc(1, 10), Doc(2, 20, "ND"), Doc(3, 30, proveedor: "SN2"), Doc(4, 40, cancelado: "S"), Doc(5, 50)
            };
            docs[1].FechaDoc = new DateTime(2026, 9, 1);   // la ND es la mas antigua
            _repoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(docs.AsAsyncQueryable());
            _saldos.Setup(s => s.ObtenerAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync((IEnumerable<int> ids) => (IReadOnlyDictionary<int, SaldoFacturaCompra>)ids.Distinct().ToDictionary(
                    i => i,
                    i => new SaldoFacturaCompra(i, 100m, 0m, i == 5 ? 100m : 0m, i == 5 ? 0m : 100m, i == 5 ? "Pagada" : "Pendiente")));

            var abiertos = await _domain.ObtenerAbiertosAsync("SN1");

            Assert.Equal(new[] { 2, 1 }, abiertos.Select(a => a.Documento.Entry).ToArray());   // 3 (otro proveedor), 4 (cancelado) y 5 (saldo 0) quedan fuera
            Assert.All(abiertos, a => Assert.True(a.Saldo.Saldo > 0m));
        }
    }
}
