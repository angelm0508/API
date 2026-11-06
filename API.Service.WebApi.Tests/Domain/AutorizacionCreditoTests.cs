using API.Domain.Core;
using API.Domain.Interface;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class AutorizacionCreditoTests
    {
        private static PosicionCredito Posicion(decimal? limite = 1000m, decimal saldo = 300m, decimal vencido = 0m,
            decimal comprometido = 500m, bool bloquear = false) =>
            new("C1", limite, bloquear, saldo, vencido, comprometido, saldo + comprometido,
                limite.HasValue ? limite.Value - (saldo + comprometido) : null);

        // ---- Excepcion ----

        [Fact]
        public void Excepcion_Limite_CodigoYMensajeLiteral()
        {
            var r = new ResultadoCredito(false, "LIMITE", Posicion(), 201m, 1m);

            var ex = new CreditoExcedidoException(r, CreditoExcedidoException.Para(r, "C1").Message);

            Assert.Equal("LIMITE_CREDITO_EXCEDIDO", ex.Codigo);
            Assert.Same(r, ex.Resultado);
            Assert.Equal("El cliente C1 excede su límite de crédito: límite 1,000.00, exposición 800.00, documento 201.00, exceso 1.00.", ex.Message);
        }

        [Fact]
        public void Excepcion_Vencido_CodigoYMensajeLiteral()
        {
            var r = new ResultadoCredito(false, "VENCIDO", Posicion(vencido: 1234.5m), 70m, 70m);

            var ex = CreditoExcedidoException.Para(r, "C1");

            Assert.Equal("CLIENTE_CON_VENCIDO", ex.Codigo);
            Assert.Equal("El cliente C1 tiene saldo vencido de 1,234.50 y está bloqueado para nuevos documentos.", ex.Message);
        }

        [Fact]
        public void Excepcion_UsaCulturaInvariante()
        {
            var anterior = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("es-AR");
                var r = new ResultadoCredito(false, "LIMITE", Posicion(), 1234.5m, 1000.25m);

                var ex = CreditoExcedidoException.Para(r, "C1");

                Assert.Contains("documento 1,234.50", ex.Message);
                Assert.Contains("exceso 1,000.25", ex.Message);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = anterior;
            }
        }

        [Fact]
        public void Excepcion_ResultadoPermitidoOSinPosicion_NoSePuedeArmar()
        {
            Assert.Throws<InvalidOperationException>(() =>
                CreditoExcedidoException.Para(new ResultadoCredito(true, null, Posicion(), 10m, 0m), "C1"));
            Assert.Throws<InvalidOperationException>(() =>
                CreditoExcedidoException.Para(new ResultadoCredito(false, "LIMITE", null, 10m, 1m), "C1"));
        }

        // ---- Fila de autorizacion ----

        [Theory]
        [InlineData(4, 17)]
        [InlineData(5, 18)]
        [InlineData(6, 19)]
        public void Registro_Crear_CopiaCifrasUsuarioMotivoYDocumento(int tipo, int entry)
        {
            var r = new ResultadoCredito(false, "LIMITE", Posicion(limite: 1000m, saldo: 300m, vencido: 25m, comprometido: 500m), 201m, 1m);
            var antes = DateTime.Now;

            var fila = AutorizacionCreditoRegistro.Crear(tipo, entry, "C1", "mgonzalez", r);

            Assert.Equal(tipo, fila.TipoDocumento);
            Assert.Equal(entry, fila.EntryDocumento);
            Assert.Equal("C1", fila.CodigoSn);
            Assert.Equal("mgonzalez", fila.Usuario);
            Assert.Equal("LIMITE", fila.Motivo);
            Assert.Equal(1000m, fila.Limite);
            Assert.Equal(300m, fila.Saldo);
            Assert.Equal(500m, fila.Comprometido);
            Assert.Equal(25m, fila.Vencido);
            Assert.Equal(201m, fila.Monto);
            Assert.Equal(1m, fila.Exceso);
            Assert.InRange(fila.Fecha, antes, DateTime.Now);
        }

        [Fact]
        public void Registro_Crear_Vencido_ConLimiteNulo()
        {
            var r = new ResultadoCredito(false, "VENCIDO", Posicion(limite: null, vencido: 10m, bloquear: true), 70m, 70m);

            var fila = AutorizacionCreditoRegistro.Crear(6, 3, "C1", "u", r);

            Assert.Equal("VENCIDO", fila.Motivo);
            Assert.Null(fila.Limite);
            Assert.Equal(10m, fila.Vencido);
            Assert.Equal(70m, fila.Exceso);
        }

        [Fact]
        public void Registro_Crear_ResultadoPermitidoOSinPosicion_Falla()
        {
            Assert.Throws<InvalidOperationException>(() =>
                AutorizacionCreditoRegistro.Crear(4, 1, "C1", "u", new ResultadoCredito(true, null, Posicion(), 10m, 0m)));
            Assert.Throws<InvalidOperationException>(() =>
                AutorizacionCreditoRegistro.Crear(4, 1, "C1", "u", new ResultadoCredito(false, "LIMITE", null, 10m, 1m)));
        }

        // ---- Contexto por peticion ----

        [Fact]
        public void Contexto_ValoresPorDefectoYAsignables()
        {
            IAutorizacionCreditoContexto ctx = new AutorizacionCreditoContexto();

            Assert.False(ctx.Autorizada);
            Assert.Null(ctx.Usuario);

            ctx.Autorizada = true;
            ctx.Usuario = "mgonzalez";

            Assert.True(ctx.Autorizada);
            Assert.Equal("mgonzalez", ctx.Usuario);
        }

        [Fact]
        public void Contexto_EsScoped_MismaInstanciaEnElScopeYDistintaEntreScopes()
        {
            var services = new ServiceCollection();
            services.AddScoped<IAutorizacionCreditoContexto, AutorizacionCreditoContexto>();
            using var provider = services.BuildServiceProvider(validateScopes: true);

            IAutorizacionCreditoContexto a1, a2, b;
            using (var scopeA = provider.CreateScope())
            {
                a1 = scopeA.ServiceProvider.GetRequiredService<IAutorizacionCreditoContexto>();
                a2 = scopeA.ServiceProvider.GetRequiredService<IAutorizacionCreditoContexto>();
                a1.Autorizada = true;
                Assert.True(a2.Autorizada);
            }
            using (var scopeB = provider.CreateScope())
            {
                b = scopeB.ServiceProvider.GetRequiredService<IAutorizacionCreditoContexto>();
            }

            Assert.Same(a1, a2);
            Assert.NotSame(a1, b);
            Assert.False(b.Autorizada);
        }
    }
}
