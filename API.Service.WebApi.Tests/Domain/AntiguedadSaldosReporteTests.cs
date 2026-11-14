using API.Domain.Core.Reportes.Definiciones;
using API.Domain.Interface;
using API.Domain.Interface.Reportes;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class AntiguedadSaldosReporteTests
    {
        private readonly Mock<IAntiguedadService> _servicio = new();

        private AntiguedadSaldosReporte Crear(string tipo = "C", params FilaAntiguedad[] filas)
        {
            _servicio.Setup(s => s.ObtenerAsync(tipo)).ReturnsAsync(filas);
            return new AntiguedadSaldosReporte(_servicio.Object);
        }

        private static FilaAntiguedad Fila(string codigo, decimal vigente = 0m, decimal d1 = 0m, decimal d31 = 0m, decimal d61 = 0m, decimal m90 = 0m) =>
            new(codigo, "Socio " + codigo, vigente, d1, d31, d61, m90, vigente + d1 + d31 + d61 + m90);

        private static FiltrosReporte Filtros(string? tipo = "C", string? socio = null)
        {
            var d = new Dictionary<string, FiltroValor>();
            if (tipo is not null) d["tipo"] = new FiltroValor { Valor = tipo };
            if (socio is not null) d["socio"] = new FiltroValor { Valor = socio };
            return new FiltrosReporte(d);
        }

        [Fact]
        public void Definicion_EsDeCobrosYPagosConSusFiltrosYColumnas()
        {
            var d = Crear().Definicion;

            Assert.Equal("antiguedad-saldos", d.Codigo);
            Assert.Equal("Antigüedad de saldos", d.Nombre);
            Assert.Equal("Saldos por cobrar o por pagar repartidos por días de vencimiento, al día de hoy.", d.Descripcion);
            Assert.Equal("Cobros y pagos", d.Familia);
            Assert.Equal("ReportesCobrosPagos", d.Modulo);
            Assert.Equal(new[] { "tipo", "socio" }, d.Filtros.Select(f => f.Campo).ToArray());

            var tipo = d.Filtros.Single(f => f.Campo == "tipo");
            Assert.Equal(TipoFiltro.Lista, tipo.Tipo);
            Assert.True(tipo.Obligatorio);
            Assert.Equal("C", tipo.ValorPorDefecto);
            Assert.Equal(new[] { "C", "P" }, tipo.Opciones!.Select(o => o.Valor).ToArray());
            Assert.Equal(new[] { "Por cobrar (clientes)", "Por pagar (proveedores)" }, tipo.Opciones!.Select(o => o.Titulo).ToArray());

            var socio = d.Filtros.Single(f => f.Campo == "socio");
            Assert.Equal(TipoFiltro.Texto, socio.Tipo);
            Assert.Equal("Código de socio", socio.Titulo);
            Assert.False(socio.Obligatorio);

            Assert.Equal(new[] { "codigoSn", "nombreSn", "vigente", "de1a30", "de31a60", "de61a90", "mas90", "total" },
                d.Columnas.Select(c => c.Campo).ToArray());
            Assert.Equal("Código", d.Columnas.Single(c => c.Campo == "codigoSn").Titulo);
            Assert.Equal("Socio", d.Columnas.Single(c => c.Campo == "nombreSn").Titulo);
            Assert.Equal("Corriente", d.Columnas.Single(c => c.Campo == "vigente").Titulo);
            Assert.Equal("1–30 días", d.Columnas.Single(c => c.Campo == "de1a30").Titulo);
            Assert.Equal("31–60", d.Columnas.Single(c => c.Campo == "de31a60").Titulo);
            Assert.Equal("61–90", d.Columnas.Single(c => c.Campo == "de61a90").Titulo);
            Assert.Equal("Más de 90", d.Columnas.Single(c => c.Campo == "mas90").Titulo);
            foreach (var campo in new[] { "vigente", "de1a30", "de31a60", "de61a90", "mas90", "total" })
            {
                Assert.Equal(TipoColumna.Dinero, d.Columnas.Single(c => c.Campo == campo).Tipo);
                Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == campo).Totaliza);
            }
            Assert.DoesNotContain(d.Columnas, c => c.Campo == "moneda");
        }

        [Fact]
        public async Task Ejecutar_MapeaCadaColumnaDeLaFilaDeAntiguedad()
        {
            var r = Crear("C", new FilaAntiguedad("C1", "Cliente Uno", 10m, 20m, 30m, 40m, 50m, 150m));

            var filas = await r.EjecutarAsync(Filtros("C"), 100);

            var f = Assert.Single(filas);
            Assert.Equal("C1", f["codigoSn"]);
            Assert.Equal("Cliente Uno", f["nombreSn"]);
            Assert.Equal(10m, f["vigente"]);
            Assert.Equal(20m, f["de1a30"]);
            Assert.Equal(30m, f["de31a60"]);
            Assert.Equal(40m, f["de61a90"]);
            Assert.Equal(50m, f["mas90"]);
            Assert.Equal(150m, f["total"]);
        }

        [Fact]
        public async Task Ejecutar_PasaElTipoAlServicio()
        {
            var r = Crear("P", Fila("P1", vigente: 5m));

            var filas = await r.EjecutarAsync(Filtros("P"), 100);

            Assert.Single(filas);
            _servicio.Verify(s => s.ObtenerAsync("P"), Times.Once);
            _servicio.Verify(s => s.ObtenerAsync("C"), Times.Never);
        }

        [Fact]
        public async Task Ejecutar_SinTipoUsaPorCobrar()
        {
            var r = Crear("C", Fila("C1", vigente: 5m));

            await r.EjecutarAsync(Filtros(tipo: null), 100);

            _servicio.Verify(s => s.ObtenerAsync("C"), Times.Once);
        }

        [Fact]
        public async Task Ejecutar_FiltraPorCodigoDeSocio()
        {
            var r = Crear("C", Fila("C1", vigente: 5m), Fila("C2", vigente: 7m), Fila("C3", vigente: 9m));

            var filas = await r.EjecutarAsync(Filtros("C", socio: "C2"), 100);

            var f = Assert.Single(filas);
            Assert.Equal("C2", f["codigoSn"]);
        }

        [Fact]
        public async Task Ejecutar_OrdenaPorTotalDescendente()
        {
            var r = Crear("C", Fila("A", vigente: 10m), Fila("B", m90: 500m), Fila("C", d1: 100m, d31: 50m));

            var filas = await r.EjecutarAsync(Filtros("C"), 100);

            Assert.Equal(new object?[] { "B", "C", "A" }, filas.Select(f => f["codigoSn"]).ToArray());
        }

        [Fact]
        public async Task Ejecutar_EmpateDeTotalDesempataPorCodigo()
        {
            var r = Crear("C", Fila("B", vigente: 10m), Fila("A", d1: 10m));

            var filas = await r.EjecutarAsync(Filtros("C"), 100);

            Assert.Equal(new object?[] { "A", "B" }, filas.Select(f => f["codigoSn"]).ToArray());
        }

        [Fact]
        public async Task Ejecutar_RespetaElLimite()
        {
            var r = Crear("C", Fila("A", vigente: 1m), Fila("B", vigente: 3m), Fila("C", vigente: 2m));

            var filas = await r.EjecutarAsync(Filtros("C"), 2);

            Assert.Equal(new object?[] { "B", "C" }, filas.Select(f => f["codigoSn"]).ToArray());
        }
    }
}
