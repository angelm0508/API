using API.Application.Main;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class EstadoCuentaApplicationTests
    {
        private readonly Mock<IEstadoCuentaService> _estado = new();
        private readonly Mock<ISaldoSocioService> _saldos = new();
        private readonly EstadoCuentaApplication _app;

        public EstadoCuentaApplicationTests()
        {
            _app = new EstadoCuentaApplication(_estado.Object, _saldos.Object);
        }

        [Fact]
        public async Task Obtener_MapeaSocioCabeceraYMovimientos()
        {
            var desde = new DateTime(2026, 1, 1);
            var hasta = new DateTime(2026, 10, 26);
            var movimientos = new List<MovimientoCuenta>
            {
                new(new DateTime(2026, 3, 1), "FAC", 10, "Factura 10", 100m, 0m, 100m, false),
                new(new DateTime(2026, 3, 5), "NC", 4, "Nota de credito 4", 0m, 0m, 100m, true),
                new(new DateTime(2026, 3, 9), "PAG", 2, "Pago 2", 0m, 40m, 60m, false)
            };
            var socio = new SocioNegocio { Codigo = "C001", Nombre = "Cliente Uno", TipoSn = "C" };
            _estado.Setup(e => e.ObtenerAsync("C001", desde, hasta))
                .ReturnsAsync(new EstadoCuenta(socio, desde, hasta, 25m, movimientos, 60m, 60m, 15m));

            var respuesta = await _app.ObtenerAsync("C001", desde, hasta);

            Assert.True(respuesta.Resultado);
            var dto = respuesta.Dato;
            Assert.Equal("C001", dto.CodigoSn);
            Assert.Equal("Cliente Uno", dto.NombreSn);
            Assert.Equal("C", dto.TipoSn);
            Assert.Equal(desde, dto.Desde);
            Assert.Equal(hasta, dto.Hasta);
            Assert.Equal(25m, dto.SaldoInicial);
            Assert.Equal(60m, dto.SaldoFinal);
            Assert.Equal(60m, dto.SaldoActual);
            Assert.Equal(15m, dto.Vencido);
            Assert.Equal(3, dto.Movimientos.Count);
            var primero = dto.Movimientos[0];
            Assert.Equal(new DateTime(2026, 3, 1), primero.Fecha);
            Assert.Equal("FAC", primero.Tipo);
            Assert.Equal(10, primero.NumDoc);
            Assert.Equal("Factura 10", primero.Descripcion);
            Assert.Equal(100m, primero.Cargo);
            Assert.Equal(0m, primero.Abono);
            Assert.Equal(100m, primero.SaldoAcumulado);
            Assert.False(primero.Informativo);
            Assert.True(dto.Movimientos[1].Informativo);
            Assert.Equal(40m, dto.Movimientos[2].Abono);
            Assert.Equal(60m, dto.Movimientos[2].SaldoAcumulado);
        }

        [Fact]
        public async Task Obtener_SinMovimientos_DevuelveListaVacia()
        {
            var socio = new SocioNegocio { Codigo = "P001", Nombre = "Proveedor", TipoSn = "P" };
            _estado.Setup(e => e.ObtenerAsync("P001", null, null))
                .ReturnsAsync(new EstadoCuenta(socio, DateTime.Today, DateTime.Today, 0m, new List<MovimientoCuenta>(), 0m, 0m, 0m));

            var respuesta = await _app.ObtenerAsync("P001", null, null);

            Assert.True(respuesta.Resultado);
            Assert.Empty(respuesta.Dato.Movimientos);
            Assert.Equal("P", respuesta.Dato.TipoSn);
        }

        [Fact]
        public async Task Obtener_SocioInexistente_DevuelveResultadoVerdaderoConDatoNulo()
        {
            _estado.Setup(e => e.ObtenerAsync("X", null, null)).ThrowsAsync(new Exception("El socio de negocio no existe."));

            var respuesta = await _app.ObtenerAsync("X", null, null);

            Assert.True(respuesta.Resultado);
            Assert.Null(respuesta.Dato);
            Assert.Equal("El socio de negocio no existe.", respuesta.Mensaje);
        }

        [Fact]
        public async Task Obtener_FechaInicialPosterior_DevuelveElMensajeSinLanzar()
        {
            var desde = new DateTime(2026, 5, 1);
            var hasta = new DateTime(2026, 4, 1);
            _estado.Setup(e => e.ObtenerAsync("C001", desde, hasta)).ThrowsAsync(new Exception("La fecha inicial no puede ser posterior a la final."));

            var respuesta = await _app.ObtenerAsync("C001", desde, hasta);

            Assert.False(respuesta.Resultado);
            Assert.Null(respuesta.Dato);
            Assert.Equal("La fecha inicial no puede ser posterior a la final.", respuesta.Mensaje);
        }

        [Fact]
        public async Task ObtenerSaldos_MapeaYOrdenaPorCodigo()
        {
            IEnumerable<string>? recibidos = null;
            _saldos.Setup(s => s.ObtenerAsync(It.IsAny<IEnumerable<string>>()))
                .Callback<IEnumerable<string>>(c => recibidos = c.ToList())
                .ReturnsAsync(new Dictionary<string, SaldoSocio>
                {
                    ["P003"] = new("P003", "P", 50m, 10m, 2),
                    ["C001"] = new("C001", "C", 120.5m, 0m, 1)
                });

            var respuesta = await _app.ObtenerSaldosAsync(new[] { "P003", "C001" });

            Assert.True(respuesta.Resultado);
            var lista = respuesta.Dato.ToList();
            Assert.Equal(new[] { "C001", "P003" }, lista.Select(s => s.CodigoSn));
            Assert.Equal("C", lista[0].Tipo);
            Assert.Equal(120.5m, lista[0].Saldo);
            Assert.Equal(0m, lista[0].Vencido);
            Assert.Equal(1, lista[0].DocumentosAbiertos);
            Assert.Equal("P", lista[1].Tipo);
            Assert.Equal(10m, lista[1].Vencido);
            Assert.Equal(2, lista[1].DocumentosAbiertos);
            Assert.Equal(new[] { "P003", "C001" }, recibidos);
        }

        [Fact]
        public async Task ObtenerSaldos_IgnoraBlancosYColapsaDuplicados()
        {
            IEnumerable<string>? recibidos = null;
            _saldos.Setup(s => s.ObtenerAsync(It.IsAny<IEnumerable<string>>()))
                .Callback<IEnumerable<string>>(c => recibidos = c.ToList())
                .ReturnsAsync(new Dictionary<string, SaldoSocio>());

            var respuesta = await _app.ObtenerSaldosAsync(new[] { "C001", "", "  ", null!, "C001", " C001 ", "C002" });

            Assert.True(respuesta.Resultado);
            Assert.Empty(respuesta.Dato);
            Assert.Equal(new[] { "C001", "C002" }, recibidos);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ObtenerSaldos_ListaNulaOVacia_DevuelveListaVaciaSinConsultar(bool nula)
        {
            var respuesta = await _app.ObtenerSaldosAsync(nula ? null : new[] { "", " " });

            Assert.True(respuesta.Resultado);
            Assert.Empty(respuesta.Dato);
            _saldos.Verify(s => s.ObtenerAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
        }

        [Fact]
        public async Task ObtenerSaldos_ExactamenteElMaximo_SeAcepta()
        {
            _saldos.Setup(s => s.ObtenerAsync(It.IsAny<IEnumerable<string>>())).ReturnsAsync(new Dictionary<string, SaldoSocio>());

            var respuesta = await _app.ObtenerSaldosAsync(Enumerable.Range(1, 500).Select(i => "C" + i));

            Assert.True(respuesta.Resultado);
        }

        [Fact]
        public async Task ObtenerSaldos_MasDeQuinientos_DevuelveErrorSinConsultar()
        {
            var respuesta = await _app.ObtenerSaldosAsync(Enumerable.Range(1, 501).Select(i => "C" + i));

            Assert.False(respuesta.Resultado);
            Assert.Equal("Demasiados socios en la consulta.", respuesta.Mensaje);
            _saldos.Verify(s => s.ObtenerAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
        }

        [Fact]
        public async Task ObtenerSaldos_ErrorInesperado_DevuelveElMensajeSinLanzar()
        {
            _saldos.Setup(s => s.ObtenerAsync(It.IsAny<IEnumerable<string>>())).ThrowsAsync(new Exception("fallo"));

            var respuesta = await _app.ObtenerSaldosAsync(new[] { "C001" });

            Assert.False(respuesta.Resultado);
            Assert.Equal("fallo", respuesta.Mensaje);
        }
    }
}
