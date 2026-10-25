using API.Domain.Core;
using API.Domain.Entity.Models;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class ValidadorMediosPagoTests
    {
        private sealed class MedioFalso : IMedioPago
        {
            public string CodFormaPago { get; set; } = null!;
            public decimal Monto { get; set; }
            public string? Referencia { get; set; }
            public string? Banco { get; set; }
            public DateTime? FechaCheque { get; set; }
        }

        private static readonly Dictionary<string, FormaPago> Formas = new()
        {
            ["EFE"] = Forma("EFE", "Efectivo", "N"),
            ["CHQ"] = Forma("CHQ", "Cheque", "C"),
            ["TRF"] = Forma("TRF", "Transferencia", "T"),
            ["TJT"] = Forma("TJT", "Tarjeta", "J"),
            ["OFF"] = Forma("OFF", "Efectivo", "N", activo: "N")
        };

        private static FormaPago Forma(string codigo, string nombre, string tipo, string activo = "S") =>
            new() { Codigo = codigo, Nombre = nombre, TipoDato = tipo, Activo = activo };

        private static MedioFalso Medio(string forma, decimal monto, string? referencia = null, string? banco = null, DateTime? fechaCheque = null) =>
            new() { CodFormaPago = forma, Monto = monto, Referencia = referencia, Banco = banco, FechaCheque = fechaCheque };

        private static Task<FormaPago?> Buscar(string codigo) =>
            Task.FromResult(Formas.TryGetValue(codigo, out var forma) ? forma : null);

        private static Task Validar(params MedioFalso[] medios) => ValidadorMediosPago.ValidarAsync(medios, Buscar);

        private static async Task<string> Mensaje(params MedioFalso[] medios) =>
            (await Assert.ThrowsAsync<Exception>(() => Validar(medios))).Message;

        [Fact]
        public async Task SinMedios_Lanza()
        {
            Assert.Equal("Indique al menos una forma de pago.", await Mensaje());
        }

        [Fact]
        public async Task FormaInexistente_Lanza()
        {
            Assert.Equal("La forma de pago XXX no existe.", await Mensaje(Medio("XXX", 100m)));
        }

        [Fact]
        public async Task FormaInactiva_Lanza()
        {
            Assert.Equal("La forma de pago Efectivo está inactiva.", await Mensaje(Medio("OFF", 100m)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task MontoNoPositivo_Lanza(int monto)
        {
            Assert.Equal("El monto de la forma de pago Efectivo debe ser mayor que 0.", await Mensaje(Medio("EFE", monto)));
        }

        [Fact]
        public async Task MontoConMasDeDosDecimales_Lanza()
        {
            Assert.Equal("El monto de la forma de pago Efectivo no puede tener más de 2 decimales.", await Mensaje(Medio("EFE", 100.001m)));
        }

        [Fact]
        public async Task ChequeSinNumero_Lanza()
        {
            Assert.Equal("La forma de pago Cheque requiere el número de cheque.", await Mensaje(Medio("CHQ", 100m, referencia: " ", banco: "Industrial")));
        }

        [Fact]
        public async Task ChequeSinBanco_Lanza()
        {
            Assert.Equal("La forma de pago Cheque requiere el banco.", await Mensaje(Medio("CHQ", 100m, referencia: "123")));
        }

        [Fact]
        public async Task TransferenciaSinReferencia_Lanza()
        {
            Assert.Equal("La forma de pago Transferencia requiere la referencia de la transferencia.", await Mensaje(Medio("TRF", 100m)));
        }

        [Fact]
        public async Task TarjetaSinAutorizacion_Lanza()
        {
            Assert.Equal("La forma de pago Tarjeta requiere la autorización o los últimos 4 dígitos de la tarjeta.", await Mensaje(Medio("TJT", 100m)));
        }

        [Fact]
        public async Task Efectivo_DescartaReferenciaBancoYFecha()
        {
            var medio = Medio("EFE", 100m, "basura", "basura", new DateTime(2026, 10, 30));

            await Validar(medio);

            Assert.Null(medio.Referencia);
            Assert.Null(medio.Banco);
            Assert.Null(medio.FechaCheque);
        }

        [Fact]
        public async Task Cheque_ConservaReferenciaBancoYFecha_RecortandoEspacios()
        {
            var medio = Medio("CHQ", 100m, " 00123 ", " Banco Industrial ", new DateTime(2026, 10, 30));

            await Validar(medio);

            Assert.Equal("00123", medio.Referencia);
            Assert.Equal("Banco Industrial", medio.Banco);
            Assert.Equal(new DateTime(2026, 10, 30), medio.FechaCheque);
        }

        [Fact]
        public async Task Transferencia_ConservaReferenciaPeroDescartaFechaDeCheque()
        {
            var medio = Medio("TRF", 100m, " ABC-1 ", null, new DateTime(2026, 10, 30));

            await Validar(medio);

            Assert.Equal("ABC-1", medio.Referencia);
            Assert.Null(medio.FechaCheque);
        }

        [Fact]
        public async Task CodigoDeFormaConEspacios_SeRecorta()
        {
            var medio = Medio(" EFE ", 100m);

            await Validar(medio);

            Assert.Equal("EFE", medio.CodFormaPago);
        }

        [Fact]
        public void Redondear_AplicaDosDecimalesAlejandoseDeCero()
        {
            Assert.Equal(1.01m, ValidadorMediosPago.Redondear(1.005m));
            Assert.Equal(-1.01m, ValidadorMediosPago.Redondear(-1.005m));
        }
    }
}
